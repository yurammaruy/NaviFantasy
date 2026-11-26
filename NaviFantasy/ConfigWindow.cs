using System;
using System.Collections.Generic;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace NaviFantasy;

public sealed class ConfigWindow : Window
{
    public const float MinScale = 0.1f;
    public const float MaxScale = 3.0f;

    private readonly Configuration config;
    private readonly Action save;
    private readonly DailyTracker tracker;

    public ConfigWindow(Configuration config, Action save, DailyTracker tracker)
        : base("Navi Fantasy###NaviFantasyConfig", ImGuiWindowFlags.AlwaysAutoResize)
    {
        this.config = config;
        this.save = save;
        this.tracker = tracker;
    }

    public override void Draw()
    {
        // ---- language
        if (ImGui.BeginCombo(Loc.Get("cfg_language"), Loc.NativeName(config.Language)))
        {
            foreach (var language in Loc.All)
            {
                if (!ImGui.Selectable(Loc.NativeName(language), language == config.Language))
                    continue;

                config.Language = language;
                Loc.Current = language;
                save();
            }

            ImGui.EndCombo();
        }

        ImGui.Separator();

        // ---- size
        var scale = config.Scale;
        ImGui.SetNextItemWidth(260f);
        if (ImGui.SliderFloat($"{Loc.Get("cfg_size")}##size", ref scale, MinScale, MaxScale, "%.2fx"))
            config.Scale = Math.Clamp(scale, MinScale, MaxScale);
        if (ImGui.IsItemDeactivatedAfterEdit())
            save();

        ImGui.Separator();

        // ---- thresholds
        var hp = config.HpAlertPercent;
        ImGui.SetNextItemWidth(260f);
        if (ImGui.SliderInt($"{Loc.Get("cfg_hp")}##hp", ref hp, 5, 95, "%d%%"))
            config.HpAlertPercent = hp;
        if (ImGui.IsItemDeactivatedAfterEdit())
            save();

        var mp = config.MpAlertPercent;
        ImGui.SetNextItemWidth(260f);
        if (ImGui.SliderInt($"{Loc.Get("cfg_mp")}##mp", ref mp, 5, 95, "%d%%"))
            config.MpAlertPercent = mp;
        if (ImGui.IsItemDeactivatedAfterEdit())
            save();

        var ally = config.AllyHealPercent;
        ImGui.SetNextItemWidth(260f);
        if (ImGui.SliderInt($"{Loc.Get("cfg_ally")}##ally", ref ally, 5, 95, "%d%%"))
            config.AllyHealPercent = ally;
        if (ImGui.IsItemDeactivatedAfterEdit())
            save();

        ImGui.Separator();

        // ---- reminders
        var dailies = config.RemindDailies;
        if (ImGui.Checkbox($"{Loc.Get("cfg_dailies")}##dailies", ref dailies))
        {
            config.RemindDailies = dailies;
            save();
        }

        if (config.RemindDailies && ImGui.CollapsingHeader($"{Loc.Get("cfg_roulettes")}##roulettes"))
        {
            var chosen = new HashSet<uint>(config.DailyRouletteIds ?? new List<uint>(tracker.DefaultIds()));
            foreach (var roulette in tracker.Roulettes)
            {
                var on = chosen.Contains(roulette.Id);
                if (!ImGui.Checkbox($"{roulette.Name}##roulette{roulette.Id}", ref on))
                    continue;

                if (on) chosen.Add(roulette.Id);
                else chosen.Remove(roulette.Id);
                config.DailyRouletteIds = new List<uint>(chosen);
                save();
            }
        }

        var mail = config.RemindMail;
        if (ImGui.Checkbox($"{Loc.Get("cfg_mail")}##mail", ref mail))
        {
            config.RemindMail = mail;
            save();
        }

        var water = config.RemindHydration;
        if (ImGui.Checkbox($"{Loc.Get("cfg_water")}##water", ref water))
        {
            config.RemindHydration = water;
            save();
        }
    }
}
