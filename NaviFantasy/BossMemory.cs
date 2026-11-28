using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Plugin.Services;

namespace NaviFantasy;

/// <summary>An announcement made before the attack starts, based on what the fairy learned in earlier fights.</summary>
public sealed record Prediction(Alert Alert, Archetype Kind, Action Commit);

/// <summary>
/// The fairy's memory of each boss. On the first fight against a boss she starts a small file for it; in every fight she
/// notes which attacks come, when (counted from the pull), where you were standing when each one landed and whether it
/// hurt you. From the next fight on she can announce an attack before it starts, remind you which ones hurt, and tell you
/// what worked last time ("stay behind the boss", "stay close to Rin", or a green mark on the radar where you were safe).
/// She learns from outcomes, so she needs no hand-written knowledge of the fight. Everything stays on this computer.
/// </summary>
public sealed class BossMemory
{
    // ------------------------------------------------------------ what is stored (one file per boss)

    /// <summary>What happened to you the last time this attack landed.</summary>
    public sealed class Outcome
    {
        public float X { get; set; }            // where you stood (arena coordinates)
        public float Z { get; set; }
        public float CasterX { get; set; }      // where the caster was
        public float CasterZ { get; set; }
        public float BossDist { get; set; }     // distance from you to the caster
        public float BossAngle { get; set; }    // 0 = in front of the caster, +-PI = behind it
        public float TargetDist { get; set; } = -1f; // distance to the ally the attack was aimed at (-1 = none)
        public bool TargetWasMe { get; set; }
        public bool Hurt { get; set; }
        public bool Killed { get; set; }
    }

    public sealed class MechanicStat
    {
        public string Key { get; set; } = string.Empty;   // "actionId#n": the n-th time this attack shows up in a fight
        public uint ActionId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Kind { get; set; } = "Unknown";
        public int Range { get; set; }
        public int Width { get; set; }
        public int Samples { get; set; }                  // in how many fights it was seen
        public double AvgStart { get; set; }              // seconds after the pull
        public double AvgCast { get; set; }
        public int Hurt { get; set; }                     // times it took a big bite out of your HP
        public int Killed { get; set; }                   // times you went down right after it
        public List<Outcome> Outcomes { get; set; } = new();
    }

    public sealed class BossModel
    {
        public uint Territory { get; set; }
        public string Boss { get; set; } = string.Empty;
        public int Encounters { get; set; }
        public int Deaths { get; set; }
        public bool Remembered { get; set; }
        public string LastSeen { get; set; } = string.Empty;
        public List<MechanicStat> Mechanics { get; set; } = new();
    }

    // ------------------------------------------------------------ what is tracked during a fight

    private sealed class Occurrence
    {
        public string Key = string.Empty;
        public uint ActionId;
        public string Name = string.Empty;
        public Archetype Kind;
        public int Range;
        public int Width;
        public double Start;            // seconds after the pull
        public float Cast;
        public double EndAbs;           // when the cast completes (absolute clock)
        public float HpAtEnd = -1f;
        public float MinHp = 101f;
        public bool Resolved;
        public bool Hurt;
        public bool Killed;

        // latest snapshot of the attack while it is being cast
        public bool HasSnapshot;
        public Vector2 CasterPos;
        public float CasterYaw;
        public Vector2? TargetPos;
        public bool TargetIsMe;

        public Outcome? Outcome;
    }

    private sealed class Encounter
    {
        public string BossName = string.Empty;
        public uint Territory;
        public double StartAbs;
        public double LastCombat;
        public double LastBoss;
        public double Offset;           // how far this fight is from the learned timeline
        public bool Died;
        public BossModel Model = new();
        public readonly List<Occurrence> Occurrences = new();
        public readonly HashSet<string> Keys = new();
        public readonly Dictionary<uint, int> Counts = new();
        public readonly Dictionary<(uint, uint), float> LastCurrent = new();
        public readonly Dictionary<(uint, uint), Occurrence> Live = new();
        public readonly HashSet<string> Predicted = new();
    }

    private const float BossHpRatio = 15f;        // an enemy with this many times your HP counts as a boss
    private const float MinCastSeconds = 1.5f;
    private const double LeadSeconds = 8d;        // announce an attack this long before it is expected
    private const double EndGraceSeconds = 4d;
    private const float HurtDrop = 20f;           // % of max HP lost right after an attack
    private const int MaxOutcomes = 12;           // how many past results are kept for each attack
    private const float SameSpotMeters = 3f;      // the caster must be this close to where it was, for a remembered spot to apply

    private readonly Configuration config;
    private readonly BossRadar radar;
    private readonly IObjectTable objectTable;
    private readonly ICondition condition;
    private readonly IClientState clientState;
    private readonly MechanicTips tips;
    private readonly IPluginLog log;
    private readonly string directory;
    private readonly JsonSerializerOptions json = new() { WriteIndented = true };
    private readonly List<(double Time, float Hp)> hpSamples = new();
    private readonly List<(double Time, Vector2 Pos)> posSamples = new();
    private readonly List<Vector2> safeSpots = new();

    private Encounter? current;
    private Alert? greeting;
    private double clock;
    private float tick;
    private float previousHp = 100f;

    public BossMemory(Configuration config, BossRadar radar, IObjectTable objectTable, ICondition condition, IClientState clientState, MechanicTips tips, string directory, IPluginLog log)
    {
        this.config = config;
        this.radar = radar;
        this.objectTable = objectTable;
        this.condition = condition;
        this.clientState = clientState;
        this.tips = tips;
        this.directory = directory;
        this.log = log;
    }

    public int KnownBossCount
    {
        get
        {
            try { return Directory.Exists(directory) ? Directory.GetFiles(directory, "*.json").Length : 0; }
            catch { return 0; }
        }
    }

    /// <summary>The one-time message for a new boss (or the "I remember you" one), until it has been delivered.</summary>
    public Alert? Greeting => greeting;

    /// <summary>Places on the floor where you were safe the last time you met the attacks that are happening now.</summary>
    public IReadOnlyList<Vector2> SafeSpots => safeSpots;

    public void GreetingDelivered() => greeting = null;

    public void Cancel()
    {
        current = null;
        greeting = null;
        safeSpots.Clear();
    }

    public void ForgetAll()
    {
        Cancel();
        try
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
        }
        catch (Exception ex)
        {
            log.Error(ex, "Could not erase what the fairy learned.");
        }
    }

    // ------------------------------------------------------------ main loop

    public void Update(float dt)
    {
        tick += dt;
        if (tick < 0.1f)
            return;
        clock += tick;
        tick = 0f;

        var player = objectTable.LocalPlayer;
        if (player == null)
        {
            Finish();
            return;
        }

        var inCombat = condition[ConditionFlag.InCombat];
        var boss = FindBoss(player);

        var encounter = current;
        if (encounter == null)
        {
            if (inCombat && boss.HasValue)
                Start(boss.Value, player);
            return;
        }

        if (inCombat)
            encounter.LastCombat = clock;
        if (boss.HasValue && boss.Value.Name == encounter.BossName)
            encounter.LastBoss = clock;

        if (clientState.TerritoryType != encounter.Territory ||
            clock - encounter.LastCombat > EndGraceSeconds ||
            clock - encounter.LastBoss > 8d)
        {
            Finish();
            return;
        }

        Observe(encounter, player);
        UpdateSafeSpots(encounter);
    }

    private HostileDot? FindBoss(IPlayerCharacter player)
    {
        HostileDot? best = null;
        var minHp = Math.Max(1u, player.MaxHp) * BossHpRatio;
        foreach (var h in radar.Hostiles)
        {
            if (!h.InCombat || h.MaxHp < minHp)
                continue;
            if (best == null || h.MaxHp > best.Value.MaxHp)
                best = h;
        }

        return best;
    }

    private void Start(HostileDot boss, IPlayerCharacter player)
    {
        var territory = clientState.TerritoryType;
        var model = Load(territory, boss.Name, out var existed);

        current = new Encounter
        {
            BossName = boss.Name,
            Territory = territory,
            StartAbs = clock,
            LastCombat = clock,
            LastBoss = clock,
            Model = model,
        };

        hpSamples.Clear();
        posSamples.Clear();
        safeSpots.Clear();
        previousHp = HpPercent(player);

        if (!existed)
        {
            // First time against this boss: from now on she is collecting data about it (this creates its file).
            Save(model);
            greeting = new Alert(AlertKind.Learning, Loc.Get("learn_first", boss.Name), 90, HudPerch.None, 5f);
        }
        else if (model.Encounters >= 1 && !model.Remembered)
        {
            model.Remembered = true;
            Save(model);
            greeting = new Alert(AlertKind.Learning, Loc.Get("learn_remember", boss.Name), 90, HudPerch.None, 5f);
        }
    }

    private static float HpPercent(IPlayerCharacter player) =>
        player.MaxHp > 0 ? 100f * player.CurrentHp / player.MaxHp : 100f;

    private void Observe(Encounter encounter, IPlayerCharacter player)
    {
        var hp = HpPercent(player);
        hpSamples.Add((clock, hp));
        posSamples.Add((clock, new Vector2(player.Position.X, player.Position.Z)));
        while (hpSamples.Count > 0 && clock - hpSamples[0].Time > 4d)
            hpSamples.RemoveAt(0);
        while (posSamples.Count > 0 && clock - posSamples[0].Time > 4d)
            posSamples.RemoveAt(0);

        // Did you go down? Blame the attack that finished most recently.
        if (hp <= 0f && previousHp > 0f)
        {
            encounter.Died = true;
            Occurrence? killer = null;
            foreach (var o in encounter.Occurrences)
                if (o.EndAbs >= clock - 4d && o.EndAbs <= clock + 0.5d && (killer == null || o.EndAbs > killer.EndAbs))
                    killer = o;
            if (killer != null)
                killer.Killed = true;
        }

        previousHp = hp;

        // New attacks, and a fresh snapshot of the ones in progress.
        var live = new HashSet<(uint, uint)>();
        foreach (var cast in radar.Casts)
        {
            if (cast.Total < MinCastSeconds)
                continue;

            var id = (cast.CasterId, cast.ActionId);
            live.Add(id);
            if (!encounter.LastCurrent.TryGetValue(id, out var before) || cast.Current + 0.05f < before)
                encounter.Live[id] = AddOccurrence(encounter, cast);
            encounter.LastCurrent[id] = cast.Current;

            if (encounter.Live.TryGetValue(id, out var occurrence))
            {
                occurrence.HasSnapshot = true;
                occurrence.CasterPos = cast.CasterPos;
                occurrence.CasterYaw = cast.CasterYaw;
                occurrence.TargetPos = cast.TargetPos;
                occurrence.TargetIsMe = cast.TargetsMe;
            }
        }

        // Forget finished casts, so the next use of the same attack counts as new.
        var finished = new List<(uint, uint)>();
        foreach (var key in encounter.LastCurrent.Keys)
            if (!live.Contains(key))
                finished.Add(key);
        foreach (var key in finished)
        {
            encounter.LastCurrent.Remove(key);
            encounter.Live.Remove(key);
        }

        foreach (var o in encounter.Occurrences)
            if (!o.Resolved)
                Resolve(o, hp);
    }

    private Occurrence AddOccurrence(Encounter encounter, RadarCast cast)
    {
        encounter.Counts.TryGetValue(cast.ActionId, out var n);
        encounter.Counts[cast.ActionId] = n + 1;

        var startAbs = clock - cast.Current;
        var occurrence = new Occurrence
        {
            Key = $"{cast.ActionId}#{n}",
            ActionId = cast.ActionId,
            Name = string.IsNullOrWhiteSpace(cast.ActionName) ? cast.ActionId.ToString(CultureInfo.InvariantCulture) : cast.ActionName,
            Kind = cast.Kind,
            Range = cast.Range,
            Width = cast.Width,
            Start = startAbs - encounter.StartAbs,
            Cast = cast.Total,
            EndAbs = startAbs + cast.Total,
        };

        encounter.Occurrences.Add(occurrence);
        encounter.Keys.Add(occurrence.Key);

        // Line this fight up with what she learned: if this attack came earlier or later than usual, everything after it will too.
        var learned = encounter.Model.Mechanics.Find(m => m.Key == occurrence.Key);
        if (learned != null)
            encounter.Offset = occurrence.Start - learned.AvgStart;

        return occurrence;
    }

    private void Resolve(Occurrence o, float hpNow)
    {
        if (clock < o.EndAbs)
            return;

        if (o.HpAtEnd < 0f)
        {
            var before = 0f;
            foreach (var s in hpSamples)
                if (s.Time >= o.EndAbs - 1d && s.Time <= o.EndAbs)
                    before = MathF.Max(before, s.Hp);
            o.HpAtEnd = before > 0f ? before : hpNow;
        }

        o.MinHp = MathF.Min(o.MinHp, hpNow);
        if (clock >= o.EndAbs + 2d)
        {
            o.Hurt = o.HpAtEnd - o.MinHp >= HurtDrop;
            o.Outcome = MakeOutcome(o);
            o.Resolved = true;
        }
    }

    /// <summary>Where you stood when the attack landed, and how you came out of it.</summary>
    private Outcome? MakeOutcome(Occurrence o)
    {
        if (!o.HasSnapshot || posSamples.Count == 0)
            return null;

        var pos = posSamples[^1].Pos;
        for (var i = posSamples.Count - 1; i >= 0; i--)
        {
            if (posSamples[i].Time <= o.EndAbs)
            {
                pos = posSamples[i].Pos;
                break;
            }
        }

        var forward = new Vector2(MathF.Sin(o.CasterYaw), MathF.Cos(o.CasterYaw));
        var d = pos - o.CasterPos;
        return new Outcome
        {
            X = pos.X,
            Z = pos.Y,
            CasterX = o.CasterPos.X,
            CasterZ = o.CasterPos.Y,
            BossDist = d.Length(),
            BossAngle = MathF.Atan2(forward.X * d.Y - forward.Y * d.X, Vector2.Dot(forward, d)),
            TargetDist = o.TargetPos.HasValue && !o.TargetIsMe ? Vector2.Distance(pos, o.TargetPos.Value) : -1f,
            TargetWasMe = o.TargetIsMe,
            Hurt = o.Hurt,
            Killed = o.Killed,
        };
    }

    // ------------------------------------------------------------ end of a fight: what she learned is saved

    private void Finish()
    {
        var encounter = current;
        current = null;
        greeting = null;
        safeSpots.Clear();
        if (encounter == null)
            return;

        var duration = clock - encounter.StartAbs;
        if (encounter.Occurrences.Count == 0 || duration < 8d)
            return;

        foreach (var o in encounter.Occurrences)
        {
            if (o.Resolved)
                continue;
            o.Hurt = o.HpAtEnd >= 0f && o.HpAtEnd - o.MinHp >= HurtDrop;
            o.Outcome ??= MakeOutcome(o);
        }

        var model = encounter.Model;
        model.Encounters++;
        model.LastSeen = DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        if (encounter.Died)
            model.Deaths++;

        foreach (var o in encounter.Occurrences)
        {
            var stat = model.Mechanics.Find(m => m.Key == o.Key);
            if (stat == null)
            {
                stat = new MechanicStat { Key = o.Key, ActionId = o.ActionId };
                model.Mechanics.Add(stat);
            }

            stat.Samples++;
            stat.AvgStart += (o.Start - stat.AvgStart) / stat.Samples;
            stat.AvgCast += (o.Cast - stat.AvgCast) / stat.Samples;
            stat.Name = o.Name;
            stat.Kind = o.Kind.ToString();
            stat.Range = o.Range;
            stat.Width = o.Width;
            if (o.Hurt) stat.Hurt++;
            if (o.Killed) stat.Killed++;

            if (o.Outcome != null)
            {
                stat.Outcomes.Add(o.Outcome);
                while (stat.Outcomes.Count > MaxOutcomes)
                    stat.Outcomes.RemoveAt(0);
            }
        }

        model.Mechanics.Sort((a, b) => a.AvgStart.CompareTo(b.AvgStart));
        Save(model);
    }

    // ------------------------------------------------------------ using what she learned

    /// <summary>The next attack she expects within a few seconds, if she knows this boss well enough.</summary>
    public Prediction? PendingPrediction(PlayerProfile me)
    {
        var encounter = current;
        if (encounter == null || encounter.Model.Encounters < 1)
            return null;

        var t = clock - encounter.StartAbs - encounter.Offset;
        foreach (var stat in encounter.Model.Mechanics)
        {
            if (encounter.Predicted.Contains(stat.Key) || encounter.Keys.Contains(stat.Key))
                continue;
            if (stat.AvgCast < 2.5)
                continue;

            var reliability = stat.Samples / (double)encounter.Model.Encounters; // how often it really shows up
            if (reliability < 0.6)
                continue;

            var eta = stat.AvgStart - t;
            if (eta < 1.5 || eta > LeadSeconds)
                continue;

            var kind = ParseKind(stat.Kind);
            var tip = PredictedTip(stat, kind, me);
            var extra = string.Join(" ", new[] { Note(stat.ActionId), Advice(stat, string.Empty, null, out _) }.Where(x => x.Length > 0));
            var sure = encounter.Model.Encounters >= 2 && reliability >= 0.8;
            var text = Loc.Get(sure ? "pred_sure" : "pred_maybe", (int)Math.Round(eta), stat.Name, (tip + " " + extra).Trim()).Trim();

            var key = stat.Key;
            var alert = new Alert(AlertKind.Prediction, text, 75, HudPerch.None, ReadingSeconds(text, 5f, 10f));
            return new Prediction(alert, kind, () => encounter.Predicted.Add(key));
        }

        return null;
    }

    /// <summary>What she remembers about an attack that is being cast right now (empty if nothing useful).</summary>
    public string Extra(RadarCast cast)
    {
        var encounter = current;
        if (encounter == null)
            return string.Empty;

        var parts = new List<string>();
        var note = Note(cast.ActionId);
        if (note.Length > 0)
            parts.Add(note);

        if (encounter.Live.TryGetValue((cast.CasterId, cast.ActionId), out var occurrence))
        {
            var stat = encounter.Model.Mechanics.Find(m => m.Key == occurrence.Key);
            if (stat != null)
            {
                var advice = Advice(stat, cast.TargetsMe ? string.Empty : cast.TargetName, cast.CasterPos, out _);
                if (advice.Length > 0)
                    parts.Add(advice);
            }
        }

        return string.Join(" ", parts);
    }

    /// <summary>"This one hurt you before", for an attack she has seen in earlier fights (empty if there is nothing to say).</summary>
    public string Note(uint actionId)
    {
        var encounter = current;
        if (encounter == null)
            return string.Empty;

        int samples = 0, hurt = 0, killed = 0;
        foreach (var stat in encounter.Model.Mechanics)
        {
            if (stat.ActionId != actionId || stat.Kind == nameof(Archetype.Raidwide))
                continue; // damage to everyone is not personal
            samples += stat.Samples;
            hurt += stat.Hurt;
            killed += stat.Killed;
        }

        if (samples >= 2 && killed > 0)
            return Loc.Get("note_killed");
        if (samples >= 2 && hurt >= 1 && hurt * 2 >= samples)
            return Loc.Get("note_hurt", hurt, samples);
        return string.Empty;
    }

    /// <summary>
    /// What worked in the past, worked out from where you stood and whether you got hurt (never from knowing the attack):
    /// stay close to / away from the ally it was aimed at, stay behind the boss, stay far from or near the boss, or simply
    /// "where you stood last time". It is always phrased as what happened last time, not as a promise.
    /// </summary>
    private string Advice(MechanicStat stat, string targetName, Vector2? casterPos, out Vector2? spot)
    {
        spot = null;
        var safe = stat.Outcomes.Where(o => !o.Hurt && !o.Killed).ToList();
        if (safe.Count == 0)
            return string.Empty;
        var bad = stat.Outcomes.Where(o => o.Hurt || o.Killed).ToList();

        // 1) An ally was the target: did staying close to them (or away from them) make the difference?
        if (!string.IsNullOrEmpty(targetName) && bad.Count > 0)
        {
            var safeT = safe.Where(o => o.TargetDist >= 0f).ToList();
            var badT = bad.Where(o => o.TargetDist >= 0f).ToList();
            if (safeT.Count > 0 && badT.Count > 0)
            {
                var safeMean = safeT.Average(o => o.TargetDist);
                var badMean = badT.Average(o => o.TargetDist);
                var first = AlertEngine.FirstName(targetName);
                if (safeMean <= 6f && badMean > safeMean + 3f)
                    return Loc.Get("learn_stack", first);
                if (safeMean >= 8f && badMean < safeMean - 3f)
                    return Loc.Get("learn_avoid", first);
            }
        }

        if (bad.Count > 0)
        {
            // 2) Hurt when in front of the boss, fine when behind or beside it.
            var frontBad = bad.Count(o => MathF.Abs(o.BossAngle) < 1.0f);
            var backSafe = safe.Count(o => MathF.Abs(o.BossAngle) > 1.8f);
            if (frontBad * 2 >= bad.Count && backSafe * 2 >= safe.Count)
                return Loc.Get("learn_behind");

            // 3) Hurt when close, fine when far (or the other way round).
            var safeDist = safe.Average(o => o.BossDist);
            var badDist = bad.Average(o => o.BossDist);
            if (safeDist > badDist + 3f)
                return Loc.Get("learn_far", (int)MathF.Floor(safeDist - 1f));
            if (safeDist < badDist - 3f)
                return Loc.Get("learn_near", (int)MathF.Ceiling(safeDist + 1f));
        }

        // 4) Otherwise just where you stood last time (only for attacks that are about the floor, and only if the boss is
        //    where it was then, so the remembered place still makes sense).
        var spatial = stat.Kind is nameof(Archetype.CircleOnCaster) or nameof(Archetype.CircleOnGround) or nameof(Archetype.Line)
            or nameof(Archetype.Cone) or nameof(Archetype.Unknown);
        if (!spatial)
            return string.Empty;

        Outcome? last = null;
        for (var i = safe.Count - 1; i >= 0; i--)
        {
            var o = safe[i];
            if (casterPos.HasValue && Vector2.Distance(new Vector2(o.CasterX, o.CasterZ), casterPos.Value) > SameSpotMeters)
                continue;
            last = o;
            break;
        }

        if (last == null)
            return string.Empty;

        var angle = MathF.Abs(last.BossAngle);
        var sector = angle < MathF.PI / 4f ? "sector_front" : angle > 3f * MathF.PI / 4f ? "sector_behind" : "sector_side";
        spot = new Vector2(last.X, last.Z);
        return Loc.Get("learn_spot", Loc.Get(sector), (int)MathF.Round(last.BossDist));
    }

    /// <summary>Marks on the radar: where you stood, unharmed, the last time each attack that is happening now landed.</summary>
    private void UpdateSafeSpots(Encounter encounter)
    {
        safeSpots.Clear();
        if (!config.RadarShowSafeSpots)
            return;

        foreach (var cast in radar.Casts)
        {
            if (!encounter.Live.TryGetValue((cast.CasterId, cast.ActionId), out var occurrence))
                continue;

            var stat = encounter.Model.Mechanics.Find(m => m.Key == occurrence.Key);
            if (stat == null)
                continue;

            Advice(stat, cast.TargetsMe ? string.Empty : cast.TargetName, cast.CasterPos, out var spot);
            if (spot.HasValue)
                safeSpots.Add(spot.Value);
        }
    }

    private string PredictedTip(MechanicStat stat, Archetype kind, PlayerProfile me)
    {
        switch (kind)
        {
            case Archetype.SingleTargetMe:
            case Archetype.SingleTargetOther:
                return me.IsHealer ? Loc.Get("tip_buster_pred_healer") : Loc.Get("tip_buster_pred_other");
            case Archetype.CircleOnTargetMe:
            case Archetype.CircleOnTargetOther:
                return Loc.Get("tip_unknown_area");
        }

        var cast = new RadarCast { ActionId = stat.ActionId, Kind = kind, Range = stat.Range, Width = stat.Width };
        return tips.Upcoming(cast, me);
    }

    private static Archetype ParseKind(string kind) =>
        Enum.TryParse<Archetype>(kind, out var parsed) ? parsed : Archetype.Unknown;

    private static float ReadingSeconds(string text, float min, float max)
    {
        var cjk = false;
        foreach (var ch in text)
            if (ch >= '\u3000')
                cjk = true;
        return Math.Clamp(3f + text.Length * (cjk ? 0.12f : 0.055f), min, max);
    }

    // ------------------------------------------------------------ files

    private string PathFor(uint territory, string boss)
    {
        var chars = boss.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
            if (!char.IsLetterOrDigit(chars[i]))
                chars[i] = '_';
        return Path.Combine(directory, $"{territory}_{new string(chars)}.json");
    }

    private BossModel Load(uint territory, string boss, out bool existed)
    {
        var path = PathFor(territory, boss);
        try
        {
            if (File.Exists(path))
            {
                var model = JsonSerializer.Deserialize<BossModel>(File.ReadAllText(path), json);
                if (model != null)
                {
                    existed = true;
                    return model;
                }
            }
        }
        catch (Exception ex)
        {
            log.Error(ex, "Could not read what the fairy learned about {Boss}.", boss);
        }

        existed = false;
        return new BossModel { Territory = territory, Boss = boss };
    }

    private void Save(BossModel model)
    {
        try
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(PathFor(model.Territory, model.Boss), JsonSerializer.Serialize(model, json));
        }
        catch (Exception ex)
        {
            log.Error(ex, "Could not save what the fairy learned about {Boss}.", model.Boss);
        }
    }
}
