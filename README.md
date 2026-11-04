# HS Deck Inspector

Pull a deck off the board in Hearthstone and turn it around in your hands.

![Rebinding the keys in Options, then spinning your deck and the opponent's](images/deck-inspector.gif)

- **D** brings out your deck, **F** your opponent's. Drag with the left mouse button to spin it.
- **D**, **F**, **Esc** or a right click sends it back.
- It's as thick as the cards left in it and wears its owner's card back, animated as in the collection.
- Its face is an unknown card: the deck's order stays hidden, and nothing here shows more than the game does.
- Change the keys in the game, under **Options > Deck Keys**.

Works in every mode with a deck, so not in Battlegrounds or Mercenaries. The game keeps running while a deck is out, the
turn timer included.

## Install

1. Download `HSDeckInspector-Setup-<version>.exe` from [Releases](../../releases).
2. Close Hearthstone and run the setup. It finds your Hearthstone folder; check it and click Install.
3. Start Hearthstone from Battle.net as usual.

The setup brings [BepInEx](https://github.com/BepInEx/BepInEx), the mod loader, if you don't have it yet. Already using
BepInEx? Put `com.tantrumua.deckinspector.dll` from [Releases](../../releases) in `BepInEx\plugins` instead.

**Requirements:** Windows, Hearthstone for PC (made for patch 37.0.2).

## License

Apache 2.0, see [LICENSE](LICENSE) and [NOTICE](NOTICE). The addon loads Hearthstone's art from your installation and
ships none of it. This project is not affiliated with Blizzard Entertainment.
