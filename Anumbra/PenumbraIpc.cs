using System;
using System.IO;
using Dalamud.Bindings.ImGui;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;

namespace Anumbra;

public sealed class PenumbraIpc : IDisposable
{
    // ── subscribers ───────────────────────────────────────────────────────────

    private readonly ICallGateSubscriber<string, float, float, object?> _preSettingsTabBarDrawGate;
    private readonly ICallGateSubscriber<string, object?>               _postEnabledDrawGate;
    private readonly ICallGateSubscriber<string, bool, object?>         _modDirChangedGate;
    private readonly ICallGateSubscriber<object?>                       _initializedGate;
    private readonly ICallGateSubscriber<Action, int>                   _registerSettingsSection;
    private readonly ICallGateSubscriber<Action, int>                   _unregisterSettingsSection;
    private readonly ICallGateSubscriber<string>                        _getModDirectory;

    // Store the exact delegate instances used for Subscribe so we can Unsubscribe later.
    private readonly Action<string, float, float> _onPreSettingsTabBarDraw;
    private readonly Action<string>               _onPostEnabledDraw;
    private readonly Action<string, bool>         _onModDirChanged;
    private readonly Action                       _onInitialized;
    private readonly Action                       _settingsSectionDelegate;

    private readonly ModImageUi    _ui;
    private readonly Configuration _config;
    private string? _cachedModRoot;

    public PenumbraIpc(IDalamudPluginInterface pi, Configuration config)
    {
        _config = config;
        _ui     = new ModImageUi(config);

        // Build wrapper delegates that swallow exceptions, matching what
        // Penumbra.Api's EventSubscriber<T> does internally.
        _onPreSettingsTabBarDraw = (dir, w, nw) =>
        {
            try { HandlePreSettingsTabBarDraw(dir, w, nw); }
            catch (Exception e) { Plugin.Log.Error($"[Anumbra] PreSettingsTabBarDraw error: {e}"); }
        };
        _onPostEnabledDraw = dir =>
        {
            try { HandlePostEnabledDraw(dir); }
            catch (Exception e) { Plugin.Log.Error($"[Anumbra] PostEnabledDraw error: {e}"); }
        };
        _onModDirChanged = (_, _) => _cachedModRoot = null;
        _onInitialized   = RegisterSettingsSection;

        _settingsSectionDelegate = DrawSettingsSection;

        _preSettingsTabBarDrawGate = pi.GetIpcSubscriber<string, float, float, object?>("Penumbra.PreSettingsTabBarDraw");
        _postEnabledDrawGate       = pi.GetIpcSubscriber<string, object?>("Penumbra.PostEnabledDraw");
        _modDirChangedGate         = pi.GetIpcSubscriber<string, bool, object?>("Penumbra.ModDirectoryChanged");
        _initializedGate           = pi.GetIpcSubscriber<object?>("Penumbra.Initialized");
        _registerSettingsSection   = pi.GetIpcSubscriber<Action, int>("PenumbraRegisterSettingsSection");
        _unregisterSettingsSection = pi.GetIpcSubscriber<Action, int>("PenumbraUnregisterSettingsSection");
        _getModDirectory           = pi.GetIpcSubscriber<string>("Penumbra.GetModDirectory");

        _preSettingsTabBarDrawGate.Subscribe(_onPreSettingsTabBarDraw);
        _postEnabledDrawGate.Subscribe(_onPostEnabledDraw);
        _modDirChangedGate.Subscribe(_onModDirChanged);
        _initializedGate.Subscribe(_onInitialized);

        RegisterSettingsSection();
    }

    // ── carousel ──────────────────────────────────────────────────────────────

    private void HandlePreSettingsTabBarDraw(string modDirectory, float totalWidth, float nameWidth)
    {
        if (!_config.ShowPreviewImages) return;
        var root = GetModRoot();
        if (root.Length == 0) return;
        _ui.DrawCarousel(Path.Combine(root, modDirectory));
    }

    // ── settings tab section ──────────────────────────────────────────────────

    private void HandlePostEnabledDraw(string modDirectory)
    {
        var root = GetModRoot();
        if (root.Length == 0) return;
        _ui.DrawPenumbraSection(Path.Combine(root, modDirectory));
    }

    // ── global settings section ───────────────────────────────────────────────

    private void RegisterSettingsSection()
    {
        try { _registerSettingsSection.InvokeFunc(_settingsSectionDelegate); } catch { }
    }

    private void DrawSettingsSection()
    {
        var show = _config.ShowPreviewImages;
        if (ImGui.Checkbox("Show preview images above the mod tab bar##AnumbraGlobal", ref show))
        {
            _config.ShowPreviewImages = show;
            _config.Save();
        }
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private string GetModRoot()
    {
        if (_cachedModRoot != null) return _cachedModRoot;
        try   { _cachedModRoot = _getModDirectory.InvokeFunc(); }
        catch (Exception e) { Plugin.Log.Warning($"[Anumbra] GetModDirectory failed: {e.Message}"); }
        return _cachedModRoot ?? string.Empty;
    }

    public void Dispose()
    {
        _preSettingsTabBarDrawGate.Unsubscribe(_onPreSettingsTabBarDraw);
        _postEnabledDrawGate.Unsubscribe(_onPostEnabledDraw);
        _modDirChangedGate.Unsubscribe(_onModDirChanged);
        _initializedGate.Unsubscribe(_onInitialized);
        try { _unregisterSettingsSection.InvokeFunc(_settingsSectionDelegate); } catch { }
        _ui.Dispose();
    }
}
