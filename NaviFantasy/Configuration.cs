using System;
using Dalamud.Configuration;

namespace NaviFantasy;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    /// <summary>Fairy size multiplier. 1.0 = default size.</summary>
    public float Scale { get; set; } = 1.0f;
}
