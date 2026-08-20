# PolyglotTerror

Shows FFXIV item and action names in up to four languages at once, inside the game's own tooltips and cast bars.

All names are read from the game client's own Excel data through Lumina, so they always match the installed game version and no translation files are bundled.

## Installation

1. In game, open Dalamud settings with `/xlsettings`
2. Go to **Experimental**
3. Under **Custom Plugin Repositories**, add:
   ```
   https://raw.githubusercontent.com/MTVirux/SeaOfTerror/main/repo.json
   ```
4. Save, then open the plugin installer with `/xlplugins` and install **PolyglotTerror**

## Usage

`/polyglot` opens the settings window.

`/polyglot nodes <AddonName>` logs an addon's node tree to the plugin log. This is only useful when a surface stops working after a game patch and you want to find the new node ids.

`/polyglot dump item` logs the next item tooltip you hover: its strings before and after the extra lines are added, and the resulting node layout. Same purpose, for tooltips rather than cast bars.

## What it covers

- Your own cast bar
- The target and focus target cast bars
- Cast bars over enemies
- The party list

The party list shows the primary language only. Everything else shows the full stack of enabled languages, in the order you set in the settings window.

## Tooltips

Item translations are shown in a panel beside the tooltip rather than inside it. Putting them in
the tooltip means relaying its header out and moving every row beneath, so the panel leaves the
game's own layout untouched entirely. It follows the tooltip and hides with it, including when you
hold Alt. Action tooltips still have their lines appended to the tooltip text itself.

By default the panel shows one language at a time, named at the top, and the scroll wheel steps
through the rest while an item is hovered. The wheel is only read, not taken, so whatever is under
the cursor still scrolls with it. Turn the option off to see every language listed at once instead.

## Languages

English, Japanese, German and French. These are the four the global client ships, so their data is always present regardless of which language the game is set to.

Korean and Chinese run on separate clients whose data is not installed alongside the global one, so they cannot be supported.

## Known limitations

The game's UI font has no CJK coverage beyond Japanese, so there is nothing to gain from adding more even if the data were there.

Fashion accessories have no description text in the game data, so only their name gets extra lines.

Node ids are game version specific. After a major patch a surface may stop adding lines until the ids are updated. It will not show wrong text - it just leaves the game's own text alone.

## Building

```
dotnet build PolyglotTerror.slnx
dotnet test tests/PolyglotTerror.Tests
```

Requires the Dalamud dev libraries at `%AppData%\XIVLauncher\addon\Hooks\dev\`, which XIVLauncher creates the first time it launches the game.

## License

MIT
