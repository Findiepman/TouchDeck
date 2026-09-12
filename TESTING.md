# Verifying TouchDeck

Unit tests cover the parsers, the config merge and the action registry. The things that can
only be checked against a real Windows desktop are listed here.

```
dotnet test
```

## The focus test (scripted)

```
powershell -ExecutionPolicy Bypass -File tools\focus-test.ps1
```

It starts the deck against a throwaway config, opens a throwaway text box, clicks three
buttons on the panel, and checks that:

- the panel window carries `WS_EX_NOACTIVATE` and `WS_EX_TOOLWINDOW`
- the foreground window is the same before and after every press
- a plain key and a key with modifiers both arrive in the window that had focus

It exits non-zero if any of that fails. No real document is touched: the target is a
disposable text box, not Notepad.

A full screen application that holds the foreground, a game in particular, stops the script
from putting its own target in front. The script says how many presses it skipped for that
reason. Close the other application and run it again.

## The focus test (by hand)

1. Start the deck.
2. Open Notepad and type a few words.
3. Press the deck button bound to `ctrl+a`, without touching the keyboard.
4. Notepad still has its title bar highlighted, and its text is now selected.

If the panel takes focus, the selection does not happen and the Notepad title bar dims.

## Hot reload

1. Start the deck.
2. Edit `%APPDATA%\TouchDeck\profiles\default.json`: change `columns`, or a label, or a
   colour in a `style` block.
3. Save. The panel redraws within about a quarter of a second, without flickering and
   without the window being recreated.

## Surviving a broken config

1. Delete a required field, or put a syntax error in a profile file, and save.
2. The deck keeps running exactly as it was, on the last configuration that loaded.
3. `%APPDATA%\TouchDeck\logs\touchdeck-<date>.log` names the file, the JSON path and the
   line. The on screen error overlay arrives in milestone 5; until then the log is where
   the message goes.
4. Fix the file and save. The panel picks the change up on the next reload.

## The config center

1. Run `TouchDeck.exe --configure`. If the deck is already running it opens the editor in
   that same process rather than starting a second one.
2. Click an empty cell on the grid. A button appears there and is selected.
3. Change its label. The grid preview updates as you type.
4. Pick a different action type. The form underneath changes to that type's own parameters,
   because it is built from what the action declares rather than from a hard coded list.
5. Drag a button onto a free cell. It moves, and refuses to land on an occupied one.
6. Press Save. Every file that changed is written, the previous version of each is kept
   next to it as `.bak`, and a running deck reloads within about a quarter of a second.

Saving rewrites only the files that changed. A rewritten file loses the comments that were
in it; the `.bak` beside it still has them.

## Closing the panel

The panel has no title bar, is not in Alt+Tab, and never takes focus, so until the tray icon
arrives in milestone 5 it is closed with:

```
TouchDeck.exe --quit
```

## Other switches

| Switch | Purpose |
| --- | --- |
| `--config <dir>` | Use a different config folder. Useful for trying a layout without disturbing the real one. |
| `--quit` | Ask a running instance to exit. |
| `--configure` | Open the config center. Works with or without the panel running. |
