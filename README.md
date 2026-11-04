# HS Deck Inspector

Pull your deck off the board in Hearthstone and turn it around in your hands.

- Press **D** in a game and your deck comes to the middle of the screen. Hold the left mouse button and drag to spin it.
- Press **F** for your opponent's deck. With one deck out, the other's key swaps them.
- **D**, **F**, **Esc** or a right click sends it back.
- The deck is as thick as the cards left in it, and wears the owner's card back, animated as in the collection.
- Its face shows an unknown card: the order of the deck stays hidden. Nothing here shows more than the game does.
- Change the keys in the game: **Options > Deck Keys**.

Works in every mode with a deck, so not in Battlegrounds or Mercenaries. The game keeps running while a deck is out, the
turn timer included, and the board takes no clicks until it's back.

## Install

**With the setup**

1. Download `HSDeckInspector-Setup-<version>.exe` from [Releases](../../releases).
2. Close Hearthstone and run the setup. It finds your Hearthstone folder; check it and click Install.
3. Start Hearthstone from Battle.net as usual.

The setup brings [BepInEx](https://github.com/BepInEx/BepInEx), the mod loader, if your Hearthstone doesn't have it yet.
If it's already there (from another mod), it's left as it is.

**By hand**

With BepInEx already installed, put `com.tantrumua.deckinspector.dll` from [Releases](../../releases) in
`BepInEx\plugins`.

**Requirements:** Windows, Hearthstone for PC (made for patch 37.0).

## Settings

Besides the keys, `BepInEx\config\com.tantrumua.deckinspector.cfg` holds the look, read again at every press: `DeckHeight`,
`DragSpeed`, `FaceStyle` (`Minion` or `Spell`), `ArtScale`, `AnimatedBack`, `FaceDepth`, `BackDepth`.

## Build

`dotnet build -c Release --no-incremental` in `plugin/` (the paths in `HsDeckInspector.csproj` point at a Hearthstone
install; close the game first). `installer\build.ps1` builds the setup and the DLL into `installer\out`; it needs
Inno Setup 6.

## License

Apache 2.0, see [LICENSE](LICENSE) and [NOTICE](NOTICE). Hearthstone's own art stays Blizzard's: the addon loads it from
your installation and ships none of it. This project is not affiliated with Blizzard Entertainment.
