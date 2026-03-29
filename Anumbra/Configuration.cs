using System;
using Dalamud.Configuration;

namespace Anumbra;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 0;

    public bool ShowPreviewImages  { get; set; } = true;
    public bool ExpandByDefault    { get; set; } = true;

    public void Save()
    {
        Plugin.PluginInterface.SavePluginConfig(this);
    }
}
