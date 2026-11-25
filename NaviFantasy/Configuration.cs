using System;
using Dalamud.Configuration;

namespace NaviFantasy;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    /// <summary>Fairy size multiplier. 0.30 is the default size.</summary>
    public float Scale { get; set; } = 0.30f;
}
