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

## Icons

Needs eyes rather than a test, because the point of it is how it looks.

1. `TouchDeck.exe --config <a throwaway folder>` writes a starter config whose buttons carry
   glyph icons. Every one should draw a real symbol, not an empty box. An empty box means
   the Segoe Fluent Icons font is missing and Segoe MDL2 Assets did not have that code point.
2. Drop a png into the icons folder and point a button at it with
   `"icon": { "type": "file", "value": "yours.png" }`. It keeps its own colours.
3. Add `"colour": "#46B96B"` to that icon. Nothing about the image changes, and the config
   center's warning list says the colour does nothing there. The config center has no colour
   field on a file icon at all; switching an icon from glyph to file drops the colour it had.
4. Set `labelPosition` on a button to each of `top`, `bottom`, `center` and `none`. Top and
   bottom pin the label to that edge, center stacks icon and label in the middle, none drops
   the label.
5. Point an icon at a file that does not exist, and at an `.svg`. Both leave the button
   showing its label, and both are named in the log and in the config center's warnings.
6. Set `backgroundImage` on the theme. It fills the screen behind the grid, cropped rather
   than stretched.
7. Set an icon size far larger than a button, say 999. The icon grows to fill the space left
   over from the label and stops there; nothing crosses the button border and no label is
   pushed off the bottom. Try it with each `labelPosition`, and with a glyph as well as an
   image.
8. In the config center, choose an image that has a white background. What lands in the icons
   folder is a png, transparent and trimmed to the picture, and the file you picked is
   untouched. Look closely at the edges on a dark button: there should be no pale outline
   where the background used to be. A photograph, or an image already cut out and filling its
   canvas, is used as it is unless it is over 512 pixels, which is reduced.
9. Set an icon to a white backgrounded image by typing its name, then press **No background**.
   A prepared png appears beside the original, the icon repoints at it, and the preview
   swatch updates. Pressing it again says there is nothing left to remove. It works the same
   on a file that was already sitting in the icons folder, which is the case that used to be
   skipped.
10. Press the ✕ beside it. The icon comes off the button and the preview empties.
11. With the icon kind set to glyph, press **Choose**. The grid opens at once, showing the
    handy glyphs first and then every glyph the font has, and keeps filling in as you scroll.
    One click on any of them sets it; no holding, no dragging.

## Changing page

The thing to watch for is a press carrying through to the page you arrive on.

1. Make a nav page with a button that switches to another page, and put a button on the
   destination page in **exactly the same square**.
2. Press the nav button and hold for a moment before lifting. The page changes as you lift,
   not as you press, and the button underneath does not fire.
3. Do it with the mouse as well as with a finger.
4. Hold the nav button down for several seconds. The page still changes when you let go.
5. Edit the config while holding a button down. The redraw waits for the release.

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
7. Press any colour swatch. One click opens the picker; one click on a colour in it sets
   that colour and closes it. Drag in the square and along the hue strip and the swatch
   follows; type into R, G and B, or into the hex box, and it follows that too. An empty
   colour means the theme decides, and there is a button in the picker that puts it back to
   that. Pressing the swatch again closes the picker rather than flickering it shut and open.
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
