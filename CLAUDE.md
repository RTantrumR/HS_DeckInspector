# HS Deck Inspector

Keys pull your deck or your opponent's off the board so you can spin it: a BepInEx plugin, released as a setup exe and a
plain DLL. Separate from HS Replay Watcher (`E:\PYPY\Test_HS_Server`), whose recon and client knowledge it reuses.

- **Client:** `E:\Games\Hearthstone`, Unity 6000.3.11 Mono x64, BepInEx 5.4.23.5, shared with the Replay Watcher's,
  Friend Search's and HS_CardExtractor's plugins; don't break their setup. Launch via Battle.net Play. The client's
  Unity modules are stripped (no `Mathf.PI`, no two-argument `Graphics.Blit`): compile against its DLLs to know what exists.
- **Layout:** `plugin/` (C#, net48; `dotnet build -c Release --no-incremental` builds into HS's `BepInEx\plugins`,
  so close HS first) · `installer/`. Plugin id `com.tantrumua.deckinspector`: the owner's plugins go by `tantrumua`.
  Version: `<Version>` in `plugin/HsDeckInspector.csproj`.
- **Docs:** `docs/` is a nested repo, ignored here, as in the sibling addons.
- **Traps:** hide the board deck with `forceRenderingOff`, never `renderer.enabled`, which gameplay reads to find the
  active thickness. The face card's base entity re-applies itself in the background: portrait through
  `SetPortraitTextureOverride`, texts under `SetBlockTextComponentUpdate`. Anything of the deck's flat back left showing
  reads as a doubled card back. See [docs/deck-inspector.md](docs/deck-inspector.md).
- **Release:** `installer\build.ps1` fills `installer\out`. Firestone's mods page takes the DLL from the latest release,
  so it goes up unzipped under its exact name, tags are plain (`1.0.0`, no `v`), and a build that adds a config
  description must keep `=` out of it. See [docs/installer.md](docs/installer.md).
