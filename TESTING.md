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

```
TouchDeck.exe --configure
```

The window is one rail, one deck and one inspector. There is no tree to navigate.

1. The deck in the middle is drawn by the same layout and theme code the panel uses, and
   takes the shape of the screen it runs on, so it is what the touchscreen will look like.
2. Press an empty square. A button appears there, the inspector opens on it, and the caret
   is already in the label box. Type a name.
3. Under "what it does", press the keys you want to send. The box records the combination
   rather than asking you to spell it.
4. Press Change to swap the action for another. The form underneath rebuilds itself from
   whatever that action declares it needs, so a new action type needs no editor changes.
5. Drag a button to move it. Drop it on another and they trade places.
6. Right click a button for copy and delete. Delete removes the selected one, Ctrl+D copies
   it, Ctrl+S saves, Escape goes back to the add surface.
7. Colours are picked from a palette. An empty colour means the theme decides, and there is
   a button in the palette that puts it back to that.
8. Press Save. Every file that changed is written, the previous version of each is kept
   next to it as `.bak`, and a running deck reloads within about a quarter of a second.

Problems are counted on the rail and only appear when there are some. Press the count to
read them.

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
