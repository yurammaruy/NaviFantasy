using System;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace NaviFantasy;

public sealed class ConfigWindow : Window
{
    public const float MinScale = 0.1f;
    public const float MaxScale = 3.0f;

    private readonly Configuration config;
    private readonly Action save;

    public ConfigWindow(Configuration config, Action save)
        : base("Navi Fantasy###NaviFantasyConfig", ImGuiWindowFlags.AlwaysAutoResize)
    {
        this.config = config;
        this.save = save;
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
    }
}
