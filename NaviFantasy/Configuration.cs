using System;
using Dalamud.Configuration;

namespace NaviFantasy;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 2;

    /// <summary>Fairy size multiplier. 0.30 is the default size.</summary>
    public float Scale { get; set; } = 0.30f;

    /// <summary>She warns you when your HP drops below this percentage.</summary>
    public int HpAlertPercent { get; set; } = 40;

    /// <summary>She warns you when your MP drops below this percentage.</summary>
    public int MpAlertPercent { get; set; } = 30;

    /// <summary>(Healers) she tells you when an ally is below this HP percentage.</summary>
    public int AllyHealPercent { get; set; } = 60;
}
