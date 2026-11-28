using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Text;
using Dalamud.Bindings.ImGui;
using Dalamud.Game;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using LuminaAction = Lumina.Excel.Sheets.Action;

namespace NaviFantasy;

/// <summary>What kind of attack an enemy is casting (decides which tip she gives).</summary>
public enum Archetype
{
    Unknown,
    Raidwide,
    CircleOnCaster,
    CircleOnTargetMe,
    CircleOnTargetOther,
    CircleOnGround,
    Line,
    Cone,
    SingleTargetMe,
    SingleTargetOther,
}

/// <summary>One enemy cast, with the area it will hit when we are able to work that out.</summary>
public sealed class RadarCast
{
    public uint CasterId;
    public string CasterName = string.Empty;
    public Vector2 CasterPos;
    public float CasterYaw;

    public uint ActionId;
    public string ActionName = string.Empty;
    public int CastType;
    public int Range;
    public int Width;
    public float Total;
    public float Current;

    public bool TargetsMe;
    public string TargetName = string.Empty;
    public Vector2? TargetPos;
    public Vector2? CastLocation;   // where the game says the attack is aimed on the floor (ground-targeted areas)

    public Archetype Kind;

    /// <summary>True when the area is known well enough to draw it and to say "you are inside".</summary>
    public bool HasZone;
    public bool IsCircle;
    public Vector2 Center;
    public float Radius;
    public bool IsRect;
    public Vector2 RectOrigin;
    public Vector2 RectForward;
    public float RectLength;
    public float RectHalfWidth;
    public bool IsCone;       // drawn approximately, never used to say "you are inside"

    public bool PlayerInside;
    public float Remaining => Total - Current;
}

public readonly record struct HostileDot(Vector2 Pos, bool Casting, string Name, uint EntityId = 0, uint MaxHp = 0, bool InCombat = false);

/// <summary>
/// The fairy's "radar": where you and the enemies are, which enemy attacks are coming, and (for the shapes we can work
/// out reliably) whether you are standing in one. It only knows what the game tells plugins, so ground-targeted and
/// complex areas are reported as "an attack is coming" without claiming you are inside them.
/// </summary>
public sealed class BossRadar
{
    private const float ScanSeconds = 0.1f;
    private const float RaidwideRange = 25f;   // circles this big cannot be dodged: treated as damage to everyone
    private const float PlayerHitbox = 0.5f;

    private readonly Configuration config;
    private readonly IObjectTable objectTable;
    private readonly IDataManager dataManager;
    private readonly IClientState clientState;
    private readonly IPluginLog log;
    private readonly string logPath;

    private readonly List<RadarCast> casts = new();
    private readonly List<HostileDot> hostiles = new();
    private readonly HashSet<string> recorded = new();
    private float scanTimer;

    public BossRadar(Configuration config, IObjectTable objectTable, IDataManager dataManager, IClientState clientState, IPluginLog log, string logPath)
    {
        this.config = config;
        this.objectTable = objectTable;
        this.dataManager = dataManager;
        this.clientState = clientState;
        this.log = log;
        this.logPath = logPath;
    }

    public bool Recording { get; set; }
    public string LogPath => logPath;

    public bool HasPlayer { get; private set; }
    public Vector2 PlayerPos { get; private set; }
    public float PlayerY { get; private set; }
    public float PlayerYaw { get; private set; }

    public IReadOnlyList<RadarCast> Casts => casts;
    public IReadOnlyList<HostileDot> Hostiles => hostiles;

    public void Update(float dt)
    {
        scanTimer += dt;
        if (scanTimer < ScanSeconds)
            return;
        scanTimer = 0f;

        casts.Clear();
        hostiles.Clear();

        var player = objectTable.LocalPlayer;
        HasPlayer = player != null;
        if (player == null)
            return;

        PlayerPos = new Vector2(player.Position.X, player.Position.Z);
        PlayerY = player.Position.Y;
        PlayerYaw = player.Rotation;

        var reach = Math.Max(10, config.RadarRangeMeters) + 25f; // a little beyond the radar: big areas start outside it
        foreach (var obj in objectTable)
        {
            if (obj is not IBattleNpc npc)
                continue;
            if ((npc.StatusFlags & StatusFlags.Hostile) == 0 || npc.IsDead)
                continue;

            var pos = new Vector2(npc.Position.X, npc.Position.Z);
            if (Vector2.Distance(pos, PlayerPos) > reach)
                continue;

            hostiles.Add(new HostileDot(pos, npc.IsCasting, npc.Name.TextValue, npc.EntityId, npc.MaxHp, (npc.StatusFlags & StatusFlags.InCombat) != 0));

            if (!npc.IsCasting || npc.CastActionType != 1)
                continue;

            var cast = Build(npc, player);
            casts.Add(cast);
            Record(cast);
        }
    }

    private RadarCast Build(IBattleNpc npc, IPlayerCharacter player)
    {
        var cast = new RadarCast
        {
            CasterId = npc.EntityId,
            CasterName = npc.Name.TextValue,
            CasterPos = new Vector2(npc.Position.X, npc.Position.Z),
            CasterYaw = npc.Rotation,
            ActionId = npc.CastActionId,
            Total = npc.TotalCastTime,
            Current = npc.CurrentCastTime,
        };

        var actions = dataManager.GetExcelSheet<LuminaAction>();
        if (actions.TryGetRow(npc.CastActionId, out var action))
        {
            cast.ActionName = action.Name.ExtractText();
            cast.CastType = action.CastType;
            cast.Range = action.EffectRange;
            cast.Width = action.XAxisModifier;
        }

        // Who is it aimed at?
        var targetId = npc.CastTargetObjectId;
        cast.TargetsMe = targetId == player.GameObjectId;
        if (cast.TargetsMe)
        {
            cast.TargetName = player.Name.TextValue;
        }
        else if (cast.CastType == 1 || cast.CastType == 2)
        {
            foreach (var other in objectTable)
            {
                if (other.GameObjectId != targetId)
                    continue;
                cast.TargetName = other.Name.TextValue;
                cast.TargetPos = new Vector2(other.Position.X, other.Position.Z);
                break;
            }
        }

        if (TryGetCastLocation(npc, out var location))
            cast.CastLocation = location;

        Classify(cast);
        return cast;
    }

    private void Classify(RadarCast c)
    {
        switch (c.CastType)
        {
            case 1: // single target
                c.Kind = c.TargetsMe ? Archetype.SingleTargetMe : Archetype.SingleTargetOther;
                break;

            case 2: // circle centered on the target
                if (c.Range >= RaidwideRange)
                {
                    c.Kind = Archetype.Raidwide;
                }
                else if (c.Range > 0 && c.TargetsMe)
                {
                    c.Kind = Archetype.CircleOnTargetMe;
                    SetCircle(c, PlayerPos, c.Range + PlayerHitbox, inside: false); // it lands on you: the answer is "spread", not "get out"
                }
                else if (c.Range > 0 && c.TargetPos.HasValue)
                {
                    c.Kind = Archetype.CircleOnTargetOther;
                    SetCircle(c, c.TargetPos.Value, c.Range + PlayerHitbox, inside: true);
                }
                else if (c.Range > 0 && c.CastLocation.HasValue)
                {
                    c.Kind = Archetype.CircleOnGround;
                    SetCircle(c, c.CastLocation.Value, c.Range + PlayerHitbox, inside: true);
                }

                break;

            case 7: // circle aimed at a spot on the floor (unconfirmed id, see README)
                if (c.Range >= RaidwideRange)
                {
                    c.Kind = Archetype.Raidwide;
                }
                else if (c.Range > 0 && c.CastLocation.HasValue)
                {
                    c.Kind = Archetype.CircleOnGround;
                    SetCircle(c, c.CastLocation.Value, c.Range + PlayerHitbox, inside: true);
                }

                break;

            case 5: // circle centered on the caster (unconfirmed id, see README)
                if (c.Range >= RaidwideRange)
                {
                    c.Kind = Archetype.Raidwide;
                }
                else if (c.Range > 0)
                {
                    c.Kind = Archetype.CircleOnCaster;
                    SetCircle(c, c.CasterPos, c.Range + PlayerHitbox, inside: true);
                }

                break;

            case 4: // line / rectangle in front of the caster
                if (c.Range > 0 && c.Width > 0)
                {
                    c.Kind = Archetype.Line;
                    c.HasZone = true;
                    c.IsRect = true;
                    c.RectOrigin = c.CasterPos;
                    c.RectForward = new Vector2(MathF.Sin(c.CasterYaw), MathF.Cos(c.CasterYaw));
                    c.RectLength = c.Range + PlayerHitbox;
                    c.RectHalfWidth = c.Width * 0.5f + PlayerHitbox;

                    var d = PlayerPos - c.RectOrigin;
                    var along = Vector2.Dot(d, c.RectForward);
                    var across = MathF.Abs(d.X * c.RectForward.Y - d.Y * c.RectForward.X);
                    c.PlayerInside = along >= -PlayerHitbox && along <= c.RectLength && across <= c.RectHalfWidth;
                }

                break;

            case 3:
            case 13: // cones: the angle is not known, so it is only drawn approximately
                c.Kind = Archetype.Cone;
                c.IsCone = c.Range > 0;
                break;

            default:
                c.Kind = Archetype.Unknown;
                break;
        }
    }

    /// <summary>The position the game stores for where this cast is aimed (read from the native cast info).</summary>
    private static unsafe bool TryGetCastLocation(IBattleNpc npc, out Vector2 location)
    {
        location = default;
        var chara = (FFXIVClientStructs.FFXIV.Client.Game.Character.BattleChara*)npc.Address;
        if (chara == null)
            return false;

        var info = chara->GetCastInfo();
        if (info == null)
            return false;

        var l = info->CastLocation;
        if (l.X == 0f && l.Z == 0f)
            return false;

        location = new Vector2(l.X, l.Z);
        return true;
    }

    private void SetCircle(RadarCast c, Vector2 center, float radius, bool inside)
    {
        c.HasZone = true;
        c.IsCircle = true;
        c.Center = center;
        c.Radius = radius;
        c.PlayerInside = inside && Vector2.Distance(PlayerPos, center) <= radius;
    }

    /// <summary>Draws the known attack areas on the floor (experimental: compare them with the game's own markers).</summary>
    public void DrawFloor(IGameGui gameGui)
    {
        if (!HasPlayer)
            return;

        var dl = ImGui.GetBackgroundDrawList();
        foreach (var c in casts)
        {
            if (!c.HasZone || c.Total < 1f)
                continue;

            var col = ImGui.ColorConvertFloat4ToU32(c.PlayerInside ? new Vector4(1f, 0.2f, 0.2f, 0.95f) : new Vector4(1f, 0.65f, 0.15f, 0.85f));

            if (c.IsCircle)
            {
                const int segments = 48;
                Vector2? previous = null;
                Vector2 first = default;
                for (var i = 0; i <= segments; i++)
                {
                    var a = i * MathF.Tau / segments;
                    var world = c.Center + new Vector2(MathF.Cos(a), MathF.Sin(a)) * c.Radius;
                    if (gameGui.WorldToScreen(new Vector3(world.X, PlayerY, world.Y), out var screen))
                    {
                        if (previous.HasValue)
                            dl.AddLine(previous.Value, screen, col, 3f);
                        previous = screen;
                        if (i == 0)
                            first = screen;
                    }
                    else
                    {
                        previous = null;
                    }
                }

                _ = first;
            }
            else if (c.IsRect)
            {
                var side = new Vector2(-c.RectForward.Y, c.RectForward.X) * c.RectHalfWidth;
                var fwd = c.RectForward * c.RectLength;
                Vector2[] corners =
                {
                    c.RectOrigin + side, c.RectOrigin + side + fwd, c.RectOrigin - side + fwd, c.RectOrigin - side,
                };

                for (var i = 0; i < 4; i++)
                {
                    var a = corners[i];
                    var b = corners[(i + 1) % 4];
                    if (gameGui.WorldToScreen(new Vector3(a.X, PlayerY, a.Y), out var sa) &&
                        gameGui.WorldToScreen(new Vector3(b.X, PlayerY, b.Y), out var sb))
                        dl.AddLine(sa, sb, col, 3f);
                }
            }
        }
    }

    // ---------------------------------------------------------------- recording (so specific tips can be written from real fights)

    private void Record(RadarCast cast)
    {
        if (!Recording)
            return;

        var territory = clientState.TerritoryType;
        var key = $"{territory}|{cast.CasterName}|{cast.ActionId}";
        if (!recorded.Add(key))
            return;

        try
        {
            var english = cast.ActionName;
            var sheet = dataManager.GetExcelSheet<LuminaAction>(ClientLanguage.English);
            if (sheet.TryGetRow(cast.ActionId, out var row))
                english = row.Name.ExtractText();

            Directory.CreateDirectory(Path.GetDirectoryName(logPath) ?? ".");
            var sb = new StringBuilder();
            if (!File.Exists(logPath))
                sb.AppendLine("territory,boss,actionId,actionName,actionNameEn,castType,range,width,castSeconds,targetsMe,archetype");

            sb.Append(territory).Append(',')
                .Append(Csv(cast.CasterName)).Append(',')
                .Append(cast.ActionId).Append(',')
                .Append(Csv(cast.ActionName)).Append(',')
                .Append(Csv(english)).Append(',')
                .Append(cast.CastType).Append(',')
                .Append(cast.Range).Append(',')
                .Append(cast.Width).Append(',')
                .Append(cast.Total.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)).Append(',')
                .Append(cast.TargetsMe ? "1" : "0").Append(',')
                .Append(cast.Kind)
                .AppendLine();

            File.AppendAllText(logPath, sb.ToString(), Encoding.UTF8);
        }
        catch (Exception ex)
        {
            log.Error(ex, "Could not write the boss log.");
        }
    }

    private static string Csv(string value) => "\"" + value.Replace("\"", "\"\"") + "\"";
}
