using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Dalamud.Plugin.Services;

namespace NaviFantasy;

/// <summary>
/// Plain-language tips for what an enemy is about to do. Generic tips come from the attack type; tips for one specific
/// attack can be added in Assets/mechanics.json (they replace the generic one):
/// [ { "action": 12345, "name": "free comment", "tip": { "pt": "...", "en": "...", "ja": "...", "de": "...", "fr": "..." } } ]
/// </summary>
public sealed class MechanicTips
{
    private sealed class Entry
    {
        public uint Action { get; set; }
        public string? Name { get; set; }
        public Dictionary<string, string>? Tip { get; set; }
    }

    private readonly Dictionary<uint, Dictionary<string, string>> custom = new();

    public MechanicTips(string path, IPluginLog log)
    {
        try
        {
            if (!File.Exists(path))
                return;

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
            };

            var entries = JsonSerializer.Deserialize<List<Entry>>(File.ReadAllText(path), options);
            if (entries == null)
                return;

            foreach (var entry in entries)
                if (entry.Action != 0 && entry.Tip is { Count: > 0 })
                    custom[entry.Action] = entry.Tip;

            log.Information("Loaded {Count} boss-specific tips.", custom.Count);
        }
        catch (Exception ex)
        {
            log.Error(ex, "Could not read mechanics.json.");
        }
    }

    private bool TryCustom(uint actionId, out string tip)
    {
        tip = string.Empty;
        if (!custom.TryGetValue(actionId, out var forms))
            return false;

        if (forms.TryGetValue(Loc.Code(Loc.Current), out var own) && !string.IsNullOrWhiteSpace(own)) { tip = own; return true; }
        if (forms.TryGetValue("en", out var en) && !string.IsNullOrWhiteSpace(en)) { tip = en; return true; }
        foreach (var any in forms.Values)
            if (!string.IsNullOrWhiteSpace(any)) { tip = any; return true; }
        return false;
    }

    /// <summary>"What is coming and how to deal with it", in one short sentence (empty when there is nothing useful to say).</summary>
    public string Upcoming(RadarCast cast, PlayerProfile me)
    {
        if (TryCustom(cast.ActionId, out var specific))
            return specific;

        var target = AlertEngine.FirstName(cast.TargetName);
        return cast.Kind switch
        {
            Archetype.CircleOnCaster => Loc.Get("tip_circle_caster"),
            Archetype.CircleOnTargetMe => Loc.Get("tip_circle_target_me"),
            Archetype.CircleOnTargetOther => Loc.Get("tip_circle_target_other", target),
            Archetype.CircleOnGround => Loc.Get("tip_circle_ground"),
            Archetype.Line => Loc.Get("tip_line"),
            Archetype.Cone => Loc.Get("tip_cone"),
            Archetype.Raidwide => me.IsHealer ? Loc.Get("tip_raidwide_healer") : Loc.Get("tip_raidwide_other"),
            Archetype.SingleTargetMe => me.IsTank ? Loc.Get("tip_buster_me_tank") : Loc.Get("tip_buster_me_other"),
            Archetype.SingleTargetOther => me.IsHealer ? Loc.Get("tip_buster_other_healer", target) : string.Empty,
            _ => Loc.Get("tip_unknown_area"),
        };
    }

    /// <summary>"Move now" advice for when you are standing inside the area.</summary>
    public string Danger(RadarCast cast)
    {
        var target = AlertEngine.FirstName(cast.TargetName);
        return cast.Kind switch
        {
            Archetype.CircleOnCaster => Loc.Get("adv_circle_caster", (int)MathF.Ceiling(cast.Radius + 1f)),
            Archetype.CircleOnTargetOther => Loc.Get("adv_circle_target_other", target),
            Archetype.CircleOnGround => Loc.Get("adv_circle_ground"),
            Archetype.Line => Loc.Get("adv_line"),
            _ => string.Empty,
        };
    }
}
