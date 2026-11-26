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
        var scale = config.Scale;
        ImGui.SetNextItemWidth(260f);
        if (ImGui.SliderFloat("Size / Tamanho", ref scale, MinScale, MaxScale, "%.2fx"))
            config.Scale = Math.Clamp(scale, MinScale, MaxScale);
        if (ImGui.IsItemDeactivatedAfterEdit())
            save();

        ImGui.Separator();

        var hp = config.HpAlertPercent;
        ImGui.SetNextItemWidth(260f);
        if (ImGui.SliderInt("Alertar HP abaixo de", ref hp, 5, 95, "%d%%"))
            config.HpAlertPercent = hp;
        if (ImGui.IsItemDeactivatedAfterEdit())
            save();

        var mp = config.MpAlertPercent;
        ImGui.SetNextItemWidth(260f);
        if (ImGui.SliderInt("Alertar mana abaixo de", ref mp, 5, 95, "%d%%"))
            config.MpAlertPercent = mp;
        if (ImGui.IsItemDeactivatedAfterEdit())
            save();

        var ally = config.AllyHealPercent;
        ImGui.SetNextItemWidth(260f);
        if (ImGui.SliderInt("Healer: aliado precisa de cura abaixo de", ref ally, 5, 95, "%d%%"))
            config.AllyHealPercent = ally;
        if (ImGui.IsItemDeactivatedAfterEdit())
            save();

        ImGui.Separator();

        var dailies = config.RemindDailies;
        if (ImGui.Checkbox("Lembrar das roletas diárias pendentes", ref dailies))
        {
            config.RemindDailies = dailies;
            save();
        }

        if (config.RemindDailies && ImGui.CollapsingHeader("Quais roletas acompanhar"))
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
        if (ImGui.Checkbox("Avisar sobre cartas não lidas no correio", ref mail))
        {
            config.RemindMail = mail;
            save();
        }

        var water = config.RemindHydration;
        if (ImGui.Checkbox("Lembrar de comer e beber (a cada 1h11)", ref water))
        {
            config.RemindHydration = water;
            save();
        }
    }
}
