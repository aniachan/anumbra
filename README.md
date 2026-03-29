# Anumbra

A [Dalamud](https://github.com/goatcorp/Dalamud) plugin for [Final Fantasy XIV](https://www.finalfantasyxiv.com/) that extends [Penumbra](https://github.com/xivdev/Penumbra) and other XIV modding tools via their public IPC — without requiring hard forks.

---

## Philosophy

Penumbra and other tools expose IPC hooks that let external plugins inject UI, react to events, and modify behaviour at runtime. Anumbra collects useful extensions that would otherwise require maintaining a full fork, keeping them decoupled from upstream releases.

---

## Current features

### Image previews for Penumbra mods

Adds a scrollable image carousel to the Penumbra mod panel. When a mod has an `images/` subfolder, the previews appear above the tab bar so they are visible on any tab.

| | |
|---|---|
| Carousel with navigation | ◄ / ► buttons, image counter, filename label |
| Upload images | File picker button always visible above the tab bar |
| WebP → PNG conversion | WebP files are automatically converted on import |
| Rename / delete | Per-image controls injected into the mod's Settings tab |
| Show/hide toggle | "Integration with Anumbra" section in Penumbra's Settings tab |

---

## Requirements

- **Penumbra** ≥ 1.6.0.0
- **Dalamud** (installed via [XIVLauncher](https://github.com/goatcorp/FFXIVQuickLauncher))

---

## Installation

> Anumbra is not yet in the official Dalamud plugin repository.

1. Open the Dalamud Plugin Installer (`/xlplugins`).
2. Go to **Settings → Experimental** and add the custom repository URL:
   ```
   https://raw.githubusercontent.com/aniachan/anumbra/main/repo.json
   ```
3. Search for **Anumbra** and install.

To load a local build as a dev plugin instead, add the output directory to the dev plugin list in `/xlsettings → Experimental`.

---

## Adding preview images to a mod

### Via the in-game upload button

1. Select any mod in Penumbra.
2. Click **+ Upload** in the image area above the tab bar.
3. Select one or more files (`.png`, `.jpg`, `.jpeg`, `.bmp`, or `.webp`). WebP is converted to PNG automatically.

### Manually

Place image files inside an `images/` subfolder at the root of the mod directory:

```
<Penumbra mod root>/
└── My Mod/
    ├── images/
    │   ├── 01_overview.png
    │   └── 02_detail.jpg
    ├── meta.json
    └── ...
```

Images are displayed in alphabetical order, so a numeric prefix controls the sequence.

---

## How it works

Anumbra subscribes to Penumbra's public IPC channels at startup and injects UI inline into Penumbra's own window — no separate windows are opened.

| IPC channel | Used for |
|-------------|----------|
| `Penumbra.PreSettingsTabBarDraw` | Carousel + upload button (above the tab bar, always visible) |
| `Penumbra.PostSettingsDraw` | Rename / delete list (bottom of the Settings tab) |
| `PenumbraRegisterSettingsSection` | Show/hide toggle in Penumbra's global Settings tab |
| `Penumbra.GetModDirectory` | Resolves the mod root path once and caches it |
| `Penumbra.ModDirectoryChanged` | Invalidates the cached mod root if the user changes it |

---

## Known limitations

- **TexTools `.ttmp2` image import** — the original fork extracted preview images embedded in modpacks during the import process. This hooks into Penumbra's internal import pipeline, which has no public IPC, so it is not supported here.
- Preview images appear **above the tab bar** rather than inside the Description tab specifically, as `PreSettingsTabBarDraw` is the only hook that fires unconditionally when a mod is selected.

---

## Building from source

```bash
git clone https://github.com/aniachan/anumbra.git
cd anumbra
dotnet build Anumbra.sln
# output: Anumbra/bin/x64/Debug/Anumbra.dll
```

Requires the [.NET 8 SDK](https://dotnet.microsoft.com/download) and a working XIVLauncher / Dalamud installation.

---

## Support

Join the [Discord](https://discord.gg/BfJvxjfvxD) for help, feedback, and updates.

---

## License

[AGPL-3.0-or-later](LICENSE.md)
