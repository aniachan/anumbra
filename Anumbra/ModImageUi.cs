using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;

namespace Anumbra;

public sealed class ModImageUi : IDisposable
{
    private readonly Configuration _config;

    private string       _modPath = string.Empty;
    private List<string> _images  = [];
    private int          _index;

    private readonly Dictionary<string, byte[]> _renameBuffers = new();
    private const int BufSize = 256;

    private static readonly string[] SupportedExtensions = [".png", ".jpg", ".jpeg", ".bmp"];

    private const float MaxW = 500f;
    private const float MaxH = 200f;

    public ModImageUi(Configuration config)
    {
        _config = config;
    }

    // ── carousel: above the tab bar ──────────────────────────────────────────
    // Kept intentionally simple (only flat ImGui calls — no tree nodes) so it
    // cannot disrupt Penumbra's header layout state.

    public void DrawCarousel(string modPath)
    {
        Sync(modPath);

        ImGui.Separator();

        if (_images.Count == 0)
        {
            ImGui.Text("No preview images.");
            ImGui.SameLine();
            if (ImGui.Button("Upload##AnumbraCarouselUpload"))
                OpenUploadDialog(modPath);
        }
        else
        {
            // nav + upload row — rendered BEFORE the image so it is never
            // pushed below the fold by Penumbra's height-capped child window.
            if (_images.Count > 1)
            {
                if (ImGui.ArrowButton("##AnumbraPrev", ImGuiDir.Left))
                    _index = (_index - 1 + _images.Count) % _images.Count;
                ImGui.SameLine();
                ImGui.Text($"{_index + 1} / {_images.Count}   {Path.GetFileNameWithoutExtension(_images[_index])}");
                ImGui.SameLine();
                if (ImGui.ArrowButton("##AnumbraNext", ImGuiDir.Right))
                    _index = (_index + 1) % _images.Count;
                ImGui.SameLine();
            }

            if (ImGui.Button("Upload##AnumbraCarouselUpload"))
                OpenUploadDialog(modPath);

            var wrap = Plugin.TextureProvider
                .GetFromFile(_images[_index])
                .GetWrapOrDefault();

            if (wrap != null)
            {
                var scale    = ImGuiHelpers.GlobalScale;
                var maxW     = Math.Min(ImGui.GetContentRegionAvail().X, MaxW * scale);
                var maxH     = MaxH * scale;
                var fitScale = Math.Min(Math.Min(maxW / wrap.Width, maxH / wrap.Height), 1f);
                ImGui.Image(wrap.Handle, new Vector2(wrap.Width * fitScale, wrap.Height * fitScale));
            }
        }

        ImGui.Separator();
    }

    // ── settings tab section: heliosphere-style TreeNode ─────────────────────
    // Injected via PostEnabledDraw — fires right after the Enabled checkbox,
    // before all mod options, so it appears at the very top of the Settings tab.
    // Right-click the section header to access per-plugin settings.

    // Mirror of heliosphere's PenumbraWindowIntegration.PostEnabledDraw
    public void DrawPenumbraSection(string modPath)
    {
        Sync(modPath);

        ImGui.PushID("anumbra-penumbra-integration");
        using var popId = new OnDispose(ImGui.PopID);

        ImGui.Spacing();

        var anyChanged = false;

        ImGui.BeginGroup();
        using (new OnDispose(ImGui.EndGroup))
        {
            var flags = _config.ExpandByDefault
                ? ImGuiTreeNodeFlags.DefaultOpen
                : ImGuiTreeNodeFlags.None;

            if (ImGui.TreeNodeEx("Anumbra", flags))
            {
                using var treePop = new OnDispose(ImGui.TreePop);

                if (ImGui.Button("Upload Preview Images##AnumbraUpload"))
                    OpenUploadDialog(modPath);

                if (_images.Count == 0)
                {
                    ImGui.SameLine();
                    ImGui.TextDisabled("(no images yet)");
                }
                else
                {
                    ImGui.Spacing();
                    for (var i = 0; i < _images.Count; i++)
                    {
                        var path = _images[i];

                        if (!_renameBuffers.TryGetValue(path, out var buf))
                        {
                            buf = new byte[BufSize];
                            Encoding.UTF8.GetBytes(Path.GetFileNameWithoutExtension(path), buf);
                            _renameBuffers[path] = buf;
                        }

                        ImGui.PushItemWidth(220f * ImGuiHelpers.GlobalScale);
                        var inputLabel = Encoding.UTF8.GetBytes($"##AnumbraRename{i}\0");
                        ImGui.InputText(inputLabel, buf, ImGuiInputTextFlags.None);
                        ImGui.PopItemWidth();

                        if (ImGui.IsItemDeactivatedAfterEdit())
                            CommitRename(i, Encoding.UTF8.GetString(buf).TrimEnd('\0'));

                        ImGui.SameLine();
                        var deleteLabel = Encoding.UTF8.GetBytes($"Delete##AnumbraDelete{i}\0");
                        if (ImGui.Button(deleteLabel))
                        {
                            try   { File.Delete(path); }
                            catch (Exception e) { Plugin.Log.Warning($"[Anumbra] Delete failed: {e.Message}"); }
                            _renameBuffers.Remove(path);
                            _images.RemoveAt(i);
                            if (_index >= _images.Count) _index = Math.Max(0, _images.Count - 1);
                            i--;
                        }
                    }
                }
            }
        }

        if (ImGui.BeginPopupContextItem("context"))
        {
            using var endPopup = new OnDispose(ImGui.EndPopup);

            var show = _config.ShowPreviewImages;
            if (ImGui.Checkbox("Show preview images above tab bar##ctx", ref show))
            {
                _config.ShowPreviewImages = show;
                anyChanged = true;
            }

            var expand = _config.ExpandByDefault;
            if (ImGui.Checkbox("Expand by default##ctx", ref expand))
            {
                _config.ExpandByDefault = expand;
                anyChanged = true;
            }
        }

        ImGui.Spacing();

        if (anyChanged)
            _config.Save();
    }

    // ── file import ──────────────────────────────────────────────────────────

    private void OpenUploadDialog(string modPath)
    {
        var imagesDir = Path.Combine(modPath, "images");
        try   { Directory.CreateDirectory(imagesDir); }
        catch (Exception e)
        {
            Plugin.Log.Warning($"[Anumbra] Could not create images dir: {e.Message}");
            return;
        }

        Plugin.FileDialogManager.OpenFileDialog(
            "Select Preview Images",
            "Images{.png,.jpg,.jpeg,.webp,.bmp}",
            (bool ok, List<string> selected) =>
            {
                if (!ok) return;
                foreach (var src in selected)
                    CopyImage(src, imagesDir);
                _modPath = string.Empty;
            },
            selectionCountMax: 0,
            startPath: null,
            isModal: false);
    }

    private void CopyImage(string src, string imagesDir)
    {
        var ext  = Path.GetExtension(src).ToLowerInvariant();
        var name = Path.GetFileNameWithoutExtension(src);
        try
        {
            if (ext == ".webp")
            {
                using var img = Image.Load(src);
                img.Save(Path.Combine(imagesDir, name + ".png"), new PngEncoder());
            }
            else
            {
                File.Copy(src, Path.Combine(imagesDir, Path.GetFileName(src)), overwrite: true);
            }
        }
        catch (Exception e)
        {
            Plugin.Log.Warning($"[Anumbra] Failed to import {src}: {e.Message}");
        }
    }

    // ── rename ────────────────────────────────────────────────────────────────

    private void CommitRename(int i, string newName)
    {
        if (string.IsNullOrWhiteSpace(newName)) return;

        var conflict = _images
            .Where((_, idx) => idx != i)
            .Any(p => string.Equals(Path.GetFileNameWithoutExtension(p), newName,
                                    StringComparison.OrdinalIgnoreCase));
        if (conflict) return;

        var oldPath = _images[i];
        var newPath = Path.Combine(Path.GetDirectoryName(oldPath)!, newName + ".png");
        if (oldPath == newPath) return;

        try
        {
            File.Move(oldPath, newPath);
            var buf = _renameBuffers[oldPath];
            _renameBuffers.Remove(oldPath);
            _renameBuffers[newPath] = buf;
            _images[i] = newPath;
        }
        catch (Exception e)
        {
            Plugin.Log.Warning($"[Anumbra] Rename failed: {e.Message}");
        }
    }

    // ── sync ─────────────────────────────────────────────────────────────────

    private void Sync(string modPath)
    {
        if (modPath == _modPath) return;
        _modPath = modPath;
        _index   = 0;
        _renameBuffers.Clear();
        _images.Clear();

        var dir = Path.Combine(modPath, "images");
        if (!Directory.Exists(dir)) return;

        try
        {
            _images = [.. Directory.GetFiles(dir)
                .Where(f => SupportedExtensions.Contains(Path.GetExtension(f).ToLower()))
                .Order()];
        }
        catch (Exception e)
        {
            Plugin.Log.Warning($"[Anumbra] Could not scan {dir}: {e.Message}");
        }
    }

    public void Dispose() { }
}
