using System;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace Anumbra;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ITextureProvider TextureProvider { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;

    // FileDialogManager is not injectable — we own the instance and call Draw() ourselves.
    internal static readonly FileDialogManager FileDialogManager = new();

    public readonly Configuration Configuration;
    private readonly PenumbraIpc _penumbraIpc;

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        _penumbraIpc  = new PenumbraIpc(PluginInterface, Configuration);
        PluginInterface.UiBuilder.Draw += FileDialogManager.Draw;
    }

    public void Dispose()
    {
        PluginInterface.UiBuilder.Draw -= FileDialogManager.Draw;
        _penumbraIpc.Dispose();
    }
}
