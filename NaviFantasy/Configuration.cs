using System;
using Dalamud.Configuration;

namespace NaviFantasy;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 2;

    /// <summary>Fairy size multiplier. 0.30 is the default size.</summary>
    public float Scale { get; set; } = 0.30f;

    /// <summary>Language of everything the plugin says and shows.</summary>
    public NaviLanguage Language { get; set; } = NaviLanguage.Portuguese;

    /// <summary>She warns you when your HP drops below this percentage.</summary>
    public int HpAlertPercent { get; set; } = 40;

    /// <summary>She warns you when your MP drops below this percentage.</summary>
    public int MpAlertPercent { get; set; } = 30;

    /// <summary>(Healers) she tells you when an ally is below this HP percentage.</summary>
    public int AllyHealPercent { get; set; } = 60;

    /// <summary>Remind about daily Duty Roulettes that are still pending.</summary>
    public bool RemindDailies { get; set; } = true;

    /// <summary>Roulette ids to monitor. Null = the default set (the ones a max-level character normally sees).</summary>
    public System.Collections.Generic.List<uint>? DailyRouletteIds { get; set; }

    /// <summary>Warn about unread letters in the Moogle mailbox.</summary>
    public bool RemindMail { get; set; } = true;

    /// <summary>Every 1h11 she reminds you to eat and drink.</summary>
    public bool RemindHydration { get; set; } = true;
}
