# HS Deck Inspector

A key pulls your deck off the board so you can spin it: a BepInEx plugin. Separate from HS Replay Watcher
(`E:\PYPY\Test_HS_Server`), whose recon and client knowledge it reuses.

- **Client:** `E:\Games\Hearthstone`, Unity 6000.3.11 Mono x64, BepInEx 5.4.23.5, shared with the Replay Watcher's,
  Friend Search's and HS_CardExtractor's plugins; don't break their setup. Launch via Battle.net Play. The client's
  Unity modules are stripped (no `Mathf.PI`, no two-argument `Graphics.Blit`): compile against its DLLs to know what exists.
- **Layout:** `plugin/` (C#, net48; `dotnet build -c Release --no-incremental` builds into HS's `BepInEx\plugins`,
  so close HS first). Version: `<Version>` in `plugin/HsDeckInspector.csproj`.
- **Repo:** local only so far, nothing on GitHub. `docs/` is a nested repo, ignored here, as in the sibling addons.
- **Traps:** hide the board deck with `forceRenderingOff`, never `renderer.enabled`, which gameplay reads to find the
  active thickness. The face card's base entity re-applies itself in the background: portrait through
  `SetPortraitTextureOverride`, texts under `SetBlockTextComponentUpdate`. See [docs/deck-inspector.md](docs/deck-inspector.md).
