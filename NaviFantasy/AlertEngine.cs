using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using LuminaAction = Lumina.Excel.Sheets.Action;
using LuminaClassJob = Lumina.Excel.Sheets.ClassJob;
using LuminaTerritoryType = Lumina.Excel.Sheets.TerritoryType;

namespace NaviFantasy;

/// <summary>Who the player is: name, job and role (1 tank, 2 melee, 3 ranged, 4 healer, 0 crafter/gatherer).</summary>
public readonly record struct PlayerProfile(string FullName, string FirstName, uint JobId, string JobAbbreviation, string JobName, int Role)
{
    public bool IsTank => Role == 1;
    public bool IsHealer => Role == 4;
    public bool IsDps => Role == 2 || Role == 3;
    public bool IsCombatJob => Role != 0;
}

/// <summary>
/// Decides when the fairy should speak. Called every frame; it only looks at the game a few times per second.
/// PvE only (the plugin does not call it in PvP).
/// </summary>
public sealed class AlertEngine
{
    private const float TickSeconds = 0.2f;
    private const float PowerfulCastSeconds = 3.0f; // enemy casts at least this long are announced
    private const float DangerMinCastSeconds = 2.0f; // ...and "you are in the area" is checked from this length
    private const float ScanRadius = 50f;           // only enemies this close (yalms)
    private const float HeyLeadSeconds = 0.85f;     // the "Hey!" that comes before every message
    private const float FactIntervalSeconds = 3600f; // one curiosity per hour spent in a safe area
    private const float HydrationIntervalSeconds = 71f * 60f; // 1h11
    private const float DailyRepeatSeconds = 2f * 3600f;      // keep reminding about pending dailies every 2 hours
    private const float MailRepeatSeconds = 3f * 3600f;       // and about unread mail every 3 hours
    // TerritoryIntendedUse ids: 0 = city ("Town"), 13 = residential district, 14 = inside a house/apartment.
    // (Use "/navi status" to see the id the game reports where you are.)
    private const uint UseTown = 0;
    private const uint UseHousingOutdoor = 13;
    private const uint UseHousingIndoor = 14;

    private sealed class CastTrack
    {
        public CastTrack(uint actionId) => ActionId = actionId;
        public uint ActionId;
        public bool Announced;
        public bool DangerAnnounced;
    }

    private sealed record Candidate(Alert Alert, float Cooldown, Action? Commit);

    private readonly Configuration config;
    private readonly IObjectTable objectTable;
    private readonly IPartyList partyList;
    private readonly IDataManager dataManager;
    private readonly ICondition condition;
    private readonly FunFacts facts;
    private readonly IClientState clientState;
    private readonly DailyTracker tracker;
    private readonly MailWatcher mail;
    private readonly BossRadar radar;
    private readonly MechanicTips tips;
    private readonly BossMemory memory;

    private readonly Dictionary<AlertKind, float> readyAt = new();
    private readonly Dictionary<string, Queue<(float Time, float Pct)>> hpHistory = new();
    private readonly Dictionary<uint, CastTrack> casts = new();

    private Alert? pendingFact;
    private float dailyClock;
    private float nextDailyAt = 60f;   // first check one minute after she appears
    private float mailClock;
    private float nextMailAt = 90f;
    private float hydrationSeconds;
    private uint cachedTerritory = uint.MaxValue;
    private uint cachedUse = uint.MaxValue;
    private float safeSeconds;
    private float clock;
    private float tickTimer;
    private float activeUntil;
    private int activePriority;

    public AlertEngine(Configuration config, IObjectTable objectTable, IPartyList partyList, IDataManager dataManager, ICondition condition, FunFacts facts, IClientState clientState, DailyTracker tracker, MailWatcher mail, BossRadar radar, MechanicTips tips, BossMemory memory)
    {
        this.config = config;
        this.objectTable = objectTable;
        this.partyList = partyList;
        this.dataManager = dataManager;
        this.condition = condition;
        this.facts = facts;
        this.clientState = clientState;
        this.tracker = tracker;
        this.mail = mail;
        this.radar = radar;
        this.tips = tips;
        this.memory = memory;
    }

    public static string FirstName(string fullName)
    {
        var i = fullName.IndexOf(' ');
        return i > 0 ? fullName[..i] : fullName;
    }

    public bool TryGetProfile(out PlayerProfile profile)
    {
        profile = default;
        var player = objectTable.LocalPlayer;
        if (player == null)
            return false;

        var full = player.Name.TextValue;
        var jobId = player.ClassJob.RowId;
        var role = 0;
        var abbreviation = "?";
        var jobName = "?";

        var sheet = dataManager.GetExcelSheet<LuminaClassJob>();
        if (sheet.TryGetRow(jobId, out var row))
        {
            role = row.Role;
            abbreviation = row.Abbreviation.ExtractText();
            jobName = row.Name.ExtractText();
        }

        profile = new PlayerProfile(full, FirstName(full), jobId, abbreviation, jobName, role);
        return true;
    }

    public void Reset()
    {
        hpHistory.Clear();
        casts.Clear();
        activeUntil = 0f;
        activePriority = 0;
    }

    /// <summary>Returns an alert to deliver now, or null.</summary>
    public Alert? Update(float dt)
    {
        clock += dt;
        tickTimer += dt;
        if (tickTimer < TickSeconds)
            return null;
        var elapsed = tickTimer;
        tickTimer = 0f;

        var player = objectTable.LocalPlayer;
        if (player == null || player.CurrentHp == 0)
        {
            casts.Clear();
            return null;
        }

        if (!TryGetProfile(out var me))
            return null;

        var candidates = new List<Candidate>();

        if (me.IsCombatJob)
        {
            AddOwnVitals(player, me, candidates);

            if (partyList.Length > 1)
            {
                if (me.IsHealer) AddHealerAlert(me, candidates);
                else hpHistory.Clear();

                if (me.IsTank) AddTankAlert(me, candidates);
            }
            else
            {
                hpHistory.Clear();
            }

            if (condition[ConditionFlag.InCombat] || condition[ConditionFlag.BoundByDuty])
                AddBossAlerts(player, me, candidates);
            else
                casts.Clear();
        }

        AddFunFact(elapsed, candidates);
        AddReminders(elapsed, me, candidates);

        Candidate? best = null;
        foreach (var c in candidates)
        {
            if (!IsReady(c.Alert.Kind))
                continue;
            if (clock < activeUntil && c.Alert.Priority <= activePriority)
                continue; // something at least as important is being said right now
            if (best == null || c.Alert.Priority > best.Alert.Priority)
                best = c;
        }

        if (best == null)
            return null;

        readyAt[best.Alert.Kind] = clock + best.Cooldown;
        activeUntil = clock + HeyLeadSeconds + best.Alert.Duration;
        activePriority = best.Alert.Priority;
        best.Commit?.Invoke();
        return best.Alert;
    }

    private bool IsReady(AlertKind kind) => !readyAt.TryGetValue(kind, out var t) || clock >= t;

    // ---------------------------------------------------------------- reminders: dailies, mail, eat & drink

    /// <summary>Daily roulettes still pending (names), using the configured selection.</summary>
    public List<string> PendingDailies() => tracker.GetPending(config.DailyRouletteIds);

    public bool HasUnreadMail() => mail.HasUnreadMail();

    public Alert MakeDailyAlert(string firstName, List<string> pending)
    {
        var shown = pending.Count <= 3
            ? string.Join(", ", pending)
            : string.Join(", ", pending.GetRange(0, 3)) + Loc.Get("and_more", pending.Count - 3);
        var text = Loc.Get("daily", firstName, shown);
        return new Alert(AlertKind.DailyTasks, text, 20, HudPerch.None, ReadingSeconds(text, 5f, 10f));
    }

    public Alert MakeMailAlert(string firstName) =>
        new(AlertKind.UnreadMail, Loc.Get("mail", firstName), 20, HudPerch.None, 5f);

    public static Alert MakeHydrationAlert()
    {
        var text = Loc.Get("hydration");
        return new Alert(AlertKind.Hydration, text, 5, HudPerch.None, ReadingSeconds(text, 4.5f, 12f));
    }

    /// <summary>How long a message stays on screen: more text, more time. Japanese needs more time per character.</summary>
    private static float ReadingSeconds(string text, float min, float max)
    {
        var cjk = false;
        foreach (var ch in text)
            if (ch >= '\u3000')
                cjk = true;

        return Math.Clamp(3f + text.Length * (cjk ? 0.12f : 0.055f), min, max);
    }

    private void AddReminders(float elapsed, PlayerProfile me, List<Candidate> list)
    {
        var inCombat = condition[ConditionFlag.InCombat];
        var calm = !inCombat && !condition[ConditionFlag.BoundByDuty];

        // Eat and drink: the clock runs the whole time you are playing; she says it as soon as you are out of combat.
        hydrationSeconds += elapsed;
        if (config.RemindHydration && hydrationSeconds >= HydrationIntervalSeconds && !inCombat)
            list.Add(new Candidate(MakeHydrationAlert(), 0f, () => hydrationSeconds = 0f));

        dailyClock += elapsed;
        mailClock += elapsed;
        if (!calm)
            return; // dailies and mail are only mentioned when she will not interrupt a fight or a duty

        if (config.RemindDailies && dailyClock >= nextDailyAt)
        {
            var pending = PendingDailies();
            if (pending.Count == 0)
            {
                nextDailyAt = dailyClock + 300f; // all done: look again in 5 minutes (the daily reset can happen while you play)
            }
            else
            {
                list.Add(new Candidate(MakeDailyAlert(me.FirstName, pending), 0f, () => nextDailyAt = dailyClock + DailyRepeatSeconds));
            }
        }

        if (config.RemindMail && mailClock >= nextMailAt)
        {
            if (!mail.HasUnreadMail())
                nextMailAt = mailClock + 60f;
            else
                list.Add(new Candidate(MakeMailAlert(me.FirstName), 0f, () => nextMailAt = mailClock + MailRepeatSeconds));
        }
    }

    // ---------------------------------------------------------------- curiosities

    /// <summary>Builds a "Você sabia?" alert from the next fact in the bag (also used by the /ykff14 command).</summary>
    public bool MakeFactAlert(out Alert alert)
    {
        alert = null!;
        if (!facts.TryNext(out var text))
            return false;

        alert = new Alert(AlertKind.Fact, text, 10, HudPerch.None, ReadingSeconds(text, 4.5f, 12f));
        return true;
    }

    /// <summary>Seconds counted so far towards the next curiosity.</summary>
    public float SafeSeconds => safeSeconds;

    /// <summary>The game's "intended use" id of the current territory (0 city, 13/14 housing, 1 open world, 3 dungeon...).</summary>
    public uint CurrentTerritoryUse()
    {
        var id = clientState.TerritoryType;
        if (id != cachedTerritory)
        {
            cachedTerritory = id;
            cachedUse = uint.MaxValue;
            var sheet = dataManager.GetExcelSheet<LuminaTerritoryType>();
            if (sheet.TryGetRow(id, out var row))
                cachedUse = row.TerritoryIntendedUse.RowId;
        }

        return cachedUse;
    }

    /// <summary>True in the main cities and in residential districts (and inside houses).</summary>
    public bool IsInSafeArea()
    {
        var use = CurrentTerritoryUse();
        return use == UseTown || use == UseHousingOutdoor || use == UseHousingIndoor;
    }

    /// <summary>
    /// Every hour spent in a safe area (main cities / residential districts, out of combat), she tells a random curiosity.
    /// The clock only runs while you are in such an area; it pauses everywhere else and only starts over after she
    /// has told a curiosity (or when the game is closed).
    /// </summary>
    private void AddFunFact(float elapsed, List<Candidate> list)
    {
        var safe = IsInSafeArea() && !condition[ConditionFlag.InCombat] && !condition[ConditionFlag.BoundByDuty];
        if (safe)
            safeSeconds += elapsed;

        if (safeSeconds < FactIntervalSeconds)
            return;

        if (pendingFact == null)
        {
            if (!MakeFactAlert(out var made))
            {
                safeSeconds = 0f; // no facts available: do not retry every tick
                return;
            }

            pendingFact = made;
        }

        var fact = pendingFact;
        list.Add(new Candidate(fact, 0f, () =>
        {
            pendingFact = null;
            safeSeconds = 0f;
        }));
    }

    // ---------------------------------------------------------------- own HP / MP (every role)

    private void AddOwnVitals(IPlayerCharacter player, PlayerProfile me, List<Candidate> list)
    {
        if (player.MaxHp > 0)
        {
            var pct = 100f * player.CurrentHp / player.MaxHp;
            if (pct < config.HpAlertPercent)
            {
                var text = Loc.Get("hp_low", me.FirstName);
                list.Add(new Candidate(new Alert(AlertKind.OwnHp, text, 70, HudPerch.Hp, 4.5f), 30f, null));
            }
        }

        if (player.MaxMp > 0)
        {
            var pct = 100f * player.CurrentMp / player.MaxMp;
            if (pct < config.MpAlertPercent)
            {
                var text = Loc.Get("mp_low", me.FirstName);
                list.Add(new Candidate(new Alert(AlertKind.OwnMp, text, 40, HudPerch.Mp, 4.5f), 30f, null));
            }
        }
    }

    // ---------------------------------------------------------------- healer: who needs healing

    private void AddHealerAlert(PlayerProfile me, List<Candidate> list)
    {
        string? worstName = null;
        var worstPct = 101f;

        foreach (var member in partyList)
        {
            var name = member.Name.TextValue;
            if (name == me.FullName || member.MaxHP == 0 || member.CurrentHP == 0)
                continue;

            var pct = 100f * member.CurrentHP / member.MaxHP;
            if (pct < worstPct)
            {
                worstPct = pct;
                worstName = name;
            }
        }

        if (worstName != null && worstPct < config.AllyHealPercent)
        {
            var text = Loc.Get("ally_heal", FirstName(worstName));
            list.Add(new Candidate(new Alert(AlertKind.AllyNeedsHeal, text, 60, HudPerch.PartyMember, 3.5f, worstName), 8f, null));
        }
    }

    // ---------------------------------------------------------------- tank: allies are taking damage

    private void AddTankAlert(PlayerProfile me, List<Candidate> list)
    {
        var seen = new HashSet<string>();
        var someoneHurt = false;

        foreach (var member in partyList)
        {
            var name = member.Name.TextValue;
            if (name == me.FullName || member.MaxHP == 0)
                continue;

            seen.Add(name);
            var pct = 100f * member.CurrentHP / member.MaxHP;

            if (!hpHistory.TryGetValue(name, out var samples))
            {
                samples = new Queue<(float Time, float Pct)>();
                hpHistory[name] = samples;
            }

            samples.Enqueue((clock, pct));
            while (samples.Count > 0 && clock - samples.Peek().Time > 3f)
                samples.Dequeue();

            var highest = pct;
            foreach (var s in samples)
                highest = MathF.Max(highest, s.Pct);

            // lost at least 10% of their max HP within the last 3 seconds (and still alive)
            if (member.CurrentHP > 0 && highest - pct >= 10f)
                someoneHurt = true;
        }

        var stale = new List<string>();
        foreach (var key in hpHistory.Keys)
            if (!seen.Contains(key))
                stale.Add(key);
        foreach (var key in stale)
            hpHistory.Remove(key);

        if (someoneHurt)
        {
            var text = Loc.Get("allies_hurt", me.FirstName);
            list.Add(new Candidate(new Alert(AlertKind.AlliesDamaged, text, 50, HudPerch.None, 4f), 20f, null));
        }
    }

    // ---------------------------------------------------------------- boss fights: what is coming, and are you in the way

    private bool UpcomingAllowed(RadarCast cast) => UpcomingAllowed(cast.Kind);

    private bool UpcomingAllowed(Archetype kind) => kind switch
    {
        Archetype.Raidwide => config.AlertRaidwide,
        Archetype.SingleTargetMe or Archetype.SingleTargetOther => config.AlertBusters,
        _ => true,
    };

    private void AddBossAlerts(IPlayerCharacter player, PlayerProfile me, List<Candidate> list)
    {
        if (!config.BossHelperEnabled)
        {
            casts.Clear();
            return;
        }

        // What the fairy learned about this boss: the one-time greeting, and attacks announced before they start.
        if (config.LearningEnabled)
        {
            if (memory.Greeting is { } greeting)
                list.Add(new Candidate(greeting, 0f, memory.GreetingDelivered));

            if (config.LearnedPredictions && config.AlertUpcoming && memory.PendingPrediction(me) is { } prediction && UpcomingAllowed(prediction.Kind))
                list.Add(new Candidate(prediction.Alert, 0f, prediction.Commit));
        }

        var seen = new HashSet<uint>();
        foreach (var cast in radar.Casts)
        {
            if (cast.Total < DangerMinCastSeconds)
                continue;

            seen.Add(cast.CasterId);
            if (!casts.TryGetValue(cast.CasterId, out var track) || track.ActionId != cast.ActionId)
            {
                track = new CastTrack(cast.ActionId);
                casts[cast.CasterId] = track;
            }

            var captured = track;
            var actionName = string.IsNullOrWhiteSpace(cast.ActionName) ? Loc.Get("strong_attack") : cast.ActionName;

            // You are standing in the area: this one cannot wait, so it skips most of the "Hey!" lead-in.
            if (config.AlertDanger && cast.PlayerInside && !captured.DangerAnnounced && cast.Remaining > 0.6f)
            {
                var text = Loc.Get("danger_now", me.FirstName, tips.Danger(cast)).Trim();
                var seconds = Math.Clamp(cast.Remaining, 2f, 4f);
                list.Add(new Candidate(new Alert(AlertKind.DangerZone, text, 100, HudPerch.None, seconds, null, true), 1.5f, () =>
                {
                    captured.DangerAnnounced = true;
                    captured.Announced = true;
                }));
            }
            else if (config.AlertUpcoming && !captured.Announced && cast.Total >= PowerfulCastSeconds && UpcomingAllowed(cast))
            {
                var tip = tips.Upcoming(cast, me);
                if (config.LearningEnabled)
                {
                    // What she learned from earlier fights ("this one hurt you before", "last time you stood behind the boss").
                    var extra = memory.Extra(cast);
                    if (extra.Length > 0)
                        tip = tip.Length + extra.Length > 150 ? extra : (tip + " " + extra).Trim(); // keep it short enough to read
                }
                var text = string.IsNullOrEmpty(tip)
                    ? Loc.Get("boss_cast", cast.CasterName, actionName)
                    : Loc.Get("boss_cast_tip", cast.CasterName, actionName, tip);
                list.Add(new Candidate(new Alert(AlertKind.BossCast, text, 80, HudPerch.None, ReadingSeconds(text, 4.5f, 10f)), 6f, () => captured.Announced = true));
            }
        }

        // forget casts that ended
        var finished = new List<uint>();
        foreach (var key in casts.Keys)
            if (!seen.Contains(key))
                finished.Add(key);
        foreach (var key in finished)
            casts.Remove(key);
    }
}
