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
    private readonly Action forgetAll;
    private bool forgetArmed;

    public ConfigWindow(Configuration config, Action save, DailyTracker tracker, Action forgetAll)
        : base("Navi Fantasy###NaviFantasyConfig", ImGuiWindowFlags.AlwaysAutoResize)
    {
        this.config = config;
        this.save = save;
        this.tracker = tracker;
        this.forgetAll = forgetAll;
    }

    public override void Draw()
    {
        if (!ImGui.BeginTabBar("##navitabs"))
            return;

        if (ImGui.BeginTabItem(Loc.Get("tab_general") + "###tabgeneral"))
        {
            DrawGeneral();
            ImGui.EndTabItem();
        }

        if (ImGui.BeginTabItem(Loc.Get("tab_boss") + "###tabboss"))
        {
            DrawBoss();
            ImGui.EndTabItem();
        }

        ImGui.EndTabBar();
    }

    private void Toggle(string labelKey, bool current, Action<bool> set, string id)
    {
        var value = current;
        if (ImGui.Checkbox($"{Loc.Get(labelKey)}##{id}", ref value))
        {
            set(value);
            save();
        }
    }

    private void IntSlider(string labelKey, int current, int min, int max, string format, Action<int> set, string id)
    {
        var value = current;
        ImGui.SetNextItemWidth(260f);
        if (ImGui.SliderInt($"{Loc.Get(labelKey)}##{id}", ref value, min, max, format))
            set(value);
        if (ImGui.IsItemDeactivatedAfterEdit())
            save();
    }

    // ------------------------------------------------------------------ general

    private void DrawGeneral()
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
        IntSlider("cfg_hp", config.HpAlertPercent, 5, 95, "%d%%", v => config.HpAlertPercent = v, "hp");
        IntSlider("cfg_mp", config.MpAlertPercent, 5, 95, "%d%%", v => config.MpAlertPercent = v, "mp");
        IntSlider("cfg_ally", config.AllyHealPercent, 5, 95, "%d%%", v => config.AllyHealPercent = v, "ally");

        ImGui.Separator();

        // ---- reminders
        Toggle("cfg_dailies", config.RemindDailies, v => config.RemindDailies = v, "dailies");

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

        Toggle("cfg_mail", config.RemindMail, v => config.RemindMail = v, "mail");
        Toggle("cfg_water", config.RemindHydration, v => config.RemindHydration = v, "water");
    }

    // ------------------------------------------------------------------ boss fights

    private void DrawBoss()
    {
        Toggle("cfg_boss_master", config.BossHelperEnabled, v => config.BossHelperEnabled = v, "bossmaster");

        ImGui.BeginDisabled(!config.BossHelperEnabled);

        ImGui.Separator();
        Toggle("cfg_radar_window", config.RadarWindowVisible, v => config.RadarWindowVisible = v, "radarwindow");
        Toggle("cfg_radar_aoe", config.RadarShowAreas, v => config.RadarShowAreas = v, "radararea");
        Toggle("cfg_radar_floor", config.RadarFloorOverlay, v => config.RadarFloorOverlay = v, "radarfloor");
        Toggle("cfg_radar_safe", config.RadarShowSafeSpots, v => config.RadarShowSafeSpots = v, "radarsafe");
        IntSlider("cfg_radar_range", config.RadarRangeMeters, 15, 60, "%d m", v => config.RadarRangeMeters = v, "radarrange");
        IntSlider("cfg_radar_size", config.RadarSizePixels, 140, 420, "%d px", v => config.RadarSizePixels = v, "radarsize");

        ImGui.Separator();
        Toggle("cfg_alert_upcoming", config.AlertUpcoming, v => config.AlertUpcoming = v, "alertupcoming");
        Toggle("cfg_alert_danger", config.AlertDanger, v => config.AlertDanger = v, "alertdanger");
        Toggle("cfg_alert_raidwide", config.AlertRaidwide, v => config.AlertRaidwide = v, "alertraidwide");
        Toggle("cfg_alert_buster", config.AlertBusters, v => config.AlertBusters = v, "alertbuster");

        ImGui.Separator();
        Toggle("cfg_learning", config.LearningEnabled, v => config.LearningEnabled = v, "learning");
        ImGui.BeginDisabled(!config.LearningEnabled);
        Toggle("cfg_predict", config.LearnedPredictions, v => config.LearnedPredictions = v, "predict");
        ImGui.EndDisabled();

        // Erasing needs two clicks, so it cannot happen by accident.
        if (ImGui.Button((forgetArmed ? Loc.Get("cfg_forget_confirm") : Loc.Get("cfg_forget")) + "##forget"))
        {
            if (forgetArmed)
            {
                forgetAll();
                forgetArmed = false;
            }
            else
            {
                forgetArmed = true;
            }
        }

        ImGui.EndDisabled();

        ImGui.Separator();
        ImGui.TextWrapped(Loc.Get("cfg_boss_note"));
    }
}
