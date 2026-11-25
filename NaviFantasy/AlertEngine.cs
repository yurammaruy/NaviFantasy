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
    private const float PowerfulCastSeconds = 3.0f; // enemy casts at least this long count as "powerful"
    private const float ScanRadius = 50f;           // only enemies this close (yalms)
    private const float HeyLeadSeconds = 0.85f;     // the "Hey!" that comes before every message
    private const float PlayerHitbox = 0.5f;
    private const float ZoneMargin = 0.5f;

    private sealed class CastTrack
    {
        public CastTrack(uint actionId) => ActionId = actionId;
        public uint ActionId;
        public bool Announced;
        public bool DangerAnnounced;
    }

    private sealed record Candidate(Alert Alert, float Cooldown, Action? Commit);

    private enum Zone { Unknown, Safe, Danger }

    private readonly Configuration config;
    private readonly IObjectTable objectTable;
    private readonly IPartyList partyList;
    private readonly IDataManager dataManager;
    private readonly ICondition condition;

    private readonly Dictionary<AlertKind, float> readyAt = new();
    private readonly Dictionary<string, Queue<(float Time, float Pct)>> hpHistory = new();
    private readonly Dictionary<uint, CastTrack> casts = new();

    private float clock;
    private float tickTimer;
    private float activeUntil;
    private int activePriority;

    public AlertEngine(Configuration config, IObjectTable objectTable, IPartyList partyList, IDataManager dataManager, ICondition condition)
    {
        this.config = config;
        this.objectTable = objectTable;
        this.partyList = partyList;
        this.dataManager = dataManager;
        this.condition = condition;
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
        tickTimer = 0f;

        var player = objectTable.LocalPlayer;
        if (player == null || player.CurrentHp == 0)
        {
            casts.Clear();
            return null;
        }

        if (!TryGetProfile(out var me) || !me.IsCombatJob)
            return null;

        var candidates = new List<Candidate>();

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

    // ---------------------------------------------------------------- own HP / MP (every role)

    private void AddOwnVitals(IPlayerCharacter player, PlayerProfile me, List<Candidate> list)
    {
        if (player.MaxHp > 0)
        {
            var pct = 100f * player.CurrentHp / player.MaxHp;
            if (pct < config.HpAlertPercent)
            {
                var text = $"{me.FirstName}, seu HP está baixo ({pct:0}%)!";
                list.Add(new Candidate(new Alert(AlertKind.OwnHp, text, 70, HudPerch.Hp, 4.5f), 30f, null));
            }
        }

        if (player.MaxMp > 0)
        {
            var pct = 100f * player.CurrentMp / player.MaxMp;
            if (pct < config.MpAlertPercent)
            {
                var text = $"{me.FirstName}, sua mana está baixa ({pct:0}%)!";
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
            var text = $"{FirstName(worstName)} precisa de cura!";
            list.Add(new Candidate(new Alert(AlertKind.AllyNeedsHeal, text, 60, HudPerch.None, 3.5f), 8f, null));
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
            var text = $"{me.FirstName}, seus aliados estão sofrendo dano!";
            list.Add(new Candidate(new Alert(AlertKind.AlliesDamaged, text, 50, HudPerch.None, 4f), 20f, null));
        }
    }

    // ---------------------------------------------------------------- enemy preparing a powerful attack / danger zone

    private void AddBossAlerts(IPlayerCharacter player, PlayerProfile me, List<Candidate> list)
    {
        var seen = new HashSet<uint>();
        var actions = dataManager.GetExcelSheet<LuminaAction>();

        foreach (var obj in objectTable)
        {
            if (obj is not IBattleNpc npc)
                continue;
            if ((npc.StatusFlags & StatusFlags.Hostile) == 0 || npc.IsDead || !npc.IsCasting) // hostile = an enemy you can fight
                continue;
            if (npc.CastActionType != 1) // 1 = a regular action (not an item, mount, etc.)
                continue;

            var total = npc.TotalCastTime;
            if (total < PowerfulCastSeconds)
                continue;
            if (Vector3.Distance(npc.Position, player.Position) > ScanRadius)
                continue;

            var id = npc.EntityId;
            seen.Add(id);
            if (!casts.TryGetValue(id, out var track) || track.ActionId != npc.CastActionId)
            {
                track = new CastTrack(npc.CastActionId);
                casts[id] = track;
            }

            var remaining = total - npc.CurrentCastTime;

            var actionName = "um golpe forte";
            var castType = 0;
            var range = 0;
            var width = 0;
            if (actions.TryGetRow(npc.CastActionId, out var action))
            {
                var name = action.Name.ExtractText();
                if (!string.IsNullOrWhiteSpace(name))
                    actionName = name;
                castType = action.CastType;
                range = action.EffectRange;
                width = action.XAxisModifier;
            }

            var zone = EvaluateZone(npc, castType, range, width, player);
            var captured = track;

            if (zone == Zone.Danger && !captured.DangerAnnounced && remaining > 0.6f)
            {
                var text = $"Sai daí, {me.FirstName}! {actionName} vai te acertar!";
                var duration = Math.Clamp(remaining, 2f, 4f);
                list.Add(new Candidate(new Alert(AlertKind.DangerZone, text, 100, HudPerch.None, duration), 1.5f, () =>
                {
                    captured.DangerAnnounced = true;
                    captured.Announced = true;
                }));
            }
            else if (!captured.Announced)
            {
                var text = $"{npc.Name.TextValue} está preparando {actionName}!";
                list.Add(new Candidate(new Alert(AlertKind.BossCast, text, 80, HudPerch.None, 4f), 6f, () => captured.Announced = true));
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

    /// <summary>
    /// Best-effort check of whether the player stands inside the area of an enemy cast.
    /// Only shapes we are sure about are evaluated; everything else is "Unknown" (the fairy still warns about the cast,
    /// but does not claim you are in the danger zone).
    /// </summary>
    private Zone EvaluateZone(IBattleNpc npc, int castType, int range, int width, IPlayerCharacter player)
    {
        var p = new Vector2(player.Position.X, player.Position.Z);
        var c = new Vector2(npc.Position.X, npc.Position.Z);
        var targetsMe = npc.CastTargetObjectId == player.GameObjectId;

        switch (castType)
        {
            case 1: // single target
                return targetsMe ? Zone.Danger : Zone.Safe;

            case 2: // circle centered on the target
            {
                if (targetsMe)
                    return Zone.Danger;

                foreach (var other in objectTable)
                {
                    if (other.GameObjectId != npc.CastTargetObjectId)
                        continue;
                    var center = new Vector2(other.Position.X, other.Position.Z);
                    return Vector2.Distance(p, center) <= range + PlayerHitbox + ZoneMargin ? Zone.Danger : Zone.Safe;
                }

                return Zone.Unknown;
            }

            case 5: // circle centered on the caster
                return Vector2.Distance(p, c) <= range + npc.HitboxRadius + PlayerHitbox + ZoneMargin ? Zone.Danger : Zone.Safe;

            case 4: // line / rectangle in front of the caster
            {
                var yaw = npc.Rotation;
                var forward = new Vector2(MathF.Sin(yaw), MathF.Cos(yaw));
                var d = p - c;
                var along = Vector2.Dot(d, forward);
                var across = MathF.Abs(d.X * forward.Y - d.Y * forward.X);
                var inside = along >= -ZoneMargin && along <= range + PlayerHitbox + ZoneMargin && across <= width * 0.5f + PlayerHitbox + ZoneMargin;
                return inside ? Zone.Danger : Zone.Safe;
            }

            default:
                return Zone.Unknown;
        }
    }
}
