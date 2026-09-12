# TouchDeck

A free Stream Deck alternative for Windows. Point it at any small touchscreen plugged in as
a second monitor and it becomes a grid of programmable buttons.

It is a native Windows application. One `.exe`, no browser, no localhost server, no Node.
Almost nothing is hardcoded: the grid, the pages, the profiles, the colours, the fonts and
the actions all live in JSON files that reload the moment you save them.

Pressing a button never takes focus away from whatever you were using, so a hotkey lands in
your game or your editor rather than in the deck.

---

## Getting started

You need Windows 10 or 11 and the .NET 9 runtime.

```
dotnet build -c Release
.\TouchDeck.App\bin\Release\net9.0-windows\TouchDeck.exe
```

The first run writes a working configuration to `%APPDATA%\TouchDeck` and puts a deck on
your second monitor.

There is no tray icon yet, and the panel has no title bar and is not in Alt+Tab, so this is
how you close it:

```
TouchDeck.exe --quit
```

### Switches

| Switch | What it does |
| --- | --- |
| `--configure` | Opens the config center. Works whether or not the deck is running. |
| `--quit` | Asks a running deck to close. |
| `--config <dir>` | Uses a different config folder, for trying something out. |

---

## Configuring it

Two ways, and they are the same thing underneath. The config center writes the files that
the deck reads, so anything you can do in one you can do in the other.

### The config center

```
TouchDeck.exe --configure
```

The deck in the middle of the window is drawn by the same code the panel uses and takes the
shape of your actual screen, so it is what the touchscreen will look like.

- Press an empty square to add a button there. The caret lands in the label box, so type.
- Under **what it does**, press the keys you want to send. It records the combination rather
  than asking you to spell it.
- Press **Change** to swap the action. The form underneath rebuilds itself from whatever
  that action needs.
- Drag a button to move it. Drop it on another and they trade places.
- Right click a button to copy or delete it. `Delete`, `Ctrl+D`, `Ctrl+S` and `Escape` work.
- Colours come from a palette. An empty colour means the theme decides.

Saving writes only the files that changed and keeps the previous version of each beside it
as `.bak`. A running deck picks the change up within about a quarter of a second.

One thing to know: a file the editor rewrites loses the comments that were in it. The `.bak`
next to it still has them.

### The files

```
%APPDATA%\TouchDeck\
  config.json          settings: which screen, how it behaves, logging
  themes.json          named themes
  profiles\*.json      one file per profile
  variables.json       values buttons have remembered
  logs\                rolling log files
```

They are JSON, but comments and trailing commas are allowed, because you edit them by hand.

---

## config.json

```json
{
  "display": {
    "select": "byName",
    "value": "\\\\.\\DISPLAY4",
    "fallbackToIndex": 0,
    "fullscreen": true,
    "bounds": null
  },
  "behaviour": { "defaultProfile": "default", "hotReload": true },
  "integrations": { "obs": { "enabled": true, "port": 4455, "password": "" } },
  "logging": { "level": "information", "retainDays": 7 }
}
```

### display

| Property | Meaning |
| --- | --- |
| `select` | `byName`, `byIndex`, `byResolution` or `primary`. |
| `value` | The device name, index or `WIDTHxHEIGHT` that `select` matches on. |
| `fallbackToIndex` | Monitor used when nothing matched, so a replug never leaves you blank. |
| `fullscreen` | Cover the whole monitor. |
| `bounds` | `{ x, y, width, height }` when `fullscreen` is false, relative to that monitor. |

Matching `byName` is the one that survives replugging. The device name looks like
`\\.\DISPLAY4`, which in JSON is written `"\\\\.\\DISPLAY4"`. Indices put the primary
monitor at 0 and the rest left to right.

### behaviour

| Property | Default | Meaning |
| --- | --- | --- |
| `defaultProfile` | `default` | Profile shown at startup. |
| `hotReload` | `true` | Reload when a file in the config folder is saved. |
| `singleInstance` | `true` | Refuse to start a second deck. |
| `actionTimeoutMs` | `10000` | An action running longer than this is cancelled and logged. |

### logging

`level` is `verbose`, `debug`, `information`, `warning`, `error` or `fatal`. `retainDays`
sets how many daily log files to keep, and takes effect at the next start.

---

## Profiles

One file per profile in `profiles\`. A profile is one grid, one theme and one or more pages.

```json
{
  "id": "streaming",
  "name": "Streaming",
  "theme": "dark",
  "grid": { "columns": 5, "rows": 3 },
  "pages": [
    { "id": "main", "buttons": [ ... ] },
    { "id": "scenes", "isFolder": true,
      "backButton": { "col": 4, "row": 2, "label": "Back",
                      "action": { "type": "closeFolder" } },
      "buttons": [ ... ] }
  ]
}
```

| Property | Meaning |
| --- | --- |
| `id` | How `switchProfile` and `defaultProfile` refer to it. |
| `name` | What you see in the config center. Falls back to the id. |
| `theme` | A name from `themes.json`. Omit for the built in look. |
| `grid.columns`, `grid.rows` | Size of the grid. |
| `grid.cellAspect` | Button width divided by height. Omit for square. |
| `grid.fill` | Stretch buttons to fill the screen instead of keeping their shape. |

### Pages

| Property | Meaning |
| --- | --- |
| `id` | How `switchPage` and `openFolder` refer to it. |
| `isFolder` | Reached from a button rather than by stepping between pages. |
| `backButton` | A button added to a folder page that returns where you came from. |

### Buttons

```json
{ "col": 0, "row": 0, "label": "Mute", "colSpan": 2,
  "style": { "background": "#5A1D1D" },
  "action": { "type": "audio", "operation": "toggleMute" } }
```

| Property | Meaning |
| --- | --- |
| `col`, `row` | Where it sits, counting from zero. Required. |
| `colSpan`, `rowSpan` | How many squares it covers. Default 1. |
| `label` | The text on it. |
| `labelPosition` | `top`, `bottom`, `center` or `none`. |
| `style` | Any subset of the theme's button block, for this button only. |
| `action` | What pressing it does. |

---

## Themes

`themes.json` holds named themes. A theme sets how every button in a profile looks, and any
button can override any single value with its own `style`.

```json
{
  "themes": {
    "dark": {
      "background": "#0B0D10",
      "gap": 8,
      "padding": 12,
      "button": {
        "background": "#171A1F",
        "backgroundPressed": "#252A32",
        "border": "#22262D",
        "borderWidth": 1,
        "cornerRadius": 12,
        "textColour": "#E8EAED",
        "textColourPressed": "#FFFFFF",
        "fontFamily": "Segoe UI Variable Display, Segoe UI",
        "fontSize": 13,
        "fontWeight": "SemiBold",
        "labelPosition": "center",
        "padding": 8
      },
      "press": { "scale": 0.94, "durationMs": 70, "easing": "cubicOut" }
    },
    "light": {
      "inherits": "dark",
      "background": "#F2F3F5",
      "button": { "background": "#FFFFFF", "textColour": "#12151A" }
    }
  }
}
```

Colours are hex, with optional alpha: `#RGB`, `#RRGGBB` or `#AARRGGBB`. A theme can
`inherit` from another and state only what differs. `press` is what a press looks like,
which on a touchscreen is the only feedback there is.

---

## Actions

Every button carries one `action`, an object with a `type` and whatever that type needs.

### Keyboard and mouse

| Type | Parameters |
| --- | --- |
| `hotkey` | `keys`, `repeat` |
| `keyDown` | `keys`. Holds them down. |
| `keyUp` | `keys`. Lets them go. |
| `text` | `value`. Types it, including characters no key produces. |
| `mouse` | `button`, `action` (`click`, `down`, `up`), `x`, `y`, `relative` |
| `scroll` | `direction` (`up`, `down`, `left`, `right`), `amount` |

Keys are written as `ctrl+shift+m`. It understands `ctrl alt shift win`, their left and
right variants, `f1` to `f24`, the numpad, media keys and punctuation. Everything goes out
as a scan code, which is why it works inside games.

### Programs and windows

| Type | Parameters |
| --- | --- |
| `launch` | `path`, `args`, `workingDir`, `focusIfRunning`, `singleInstance` |
| `open` | `url` |
| `shell` | `command`, `shell` (`powerShell`, `cmd`), `hidden`, `captureOutputTo` |
| `window` | `operation` (`focus`, `minimise`, `maximise`, `restore`, `close`), `processName`, `titleRegex` |
| `clipboard` | `operation` (`set`, `get`, `clear`), `value`, `into` |

### Sound

| Type | Parameters |
| --- | --- |
| `audio` | `target` (`default`, `device`, `process`), `name`, `operation` (`mute`, `unmute`, `toggleMute`, `set`, `adjust`), `value` |
| `media` | `command` (`playPause`, `next`, `previous`, `stop`) |

`value` is 0 to 1 when setting, or the amount to move by when adjusting.

### OBS

| Type | Parameters |
| --- | --- |
| `obs` | `command`, `scene`, `input`, `source`, `filter`, `enabled`, `volume` |

`command` is one of `setScene`, `toggleInputMute`, `setInputVolume`, `startStream`,
`stopStream`, `toggleStream`, `startRecord`, `stopRecord`, `toggleRecord`, `pauseRecord`,
`resumeRecord`, `saveReplayBuffer`, `setFilterEnabled`, `setSourceVisible`.

Switch it on under `integrations.obs` in `config.json` and turn on the websocket server in
OBS under Tools. The deck reconnects on its own, waiting longer each time, so starting OBS
later sorts itself out.

### Moving around the deck

| Type | Parameters |
| --- | --- |
| `switchProfile` | `profile` |
| `switchPage` | `page`, or the word `next` or `previous` |
| `openFolder` | `page` |
| `closeFolder` | none |

### Remembering things

| Type | Parameters |
| --- | --- |
| `setVariable` | `name`, `value`, `scope` (`session`, `persistent`) |
| `toggleVariable` | `name` |

Persistent values are kept in `variables.json` and are still there next time.

### Doing several things

| Type | Parameters |
| --- | --- |
| `sequence` | `steps`, an array of actions, and `stopOnError` |
| `delay` | `ms` |
| `conditional` | `if`, `then`, `else` |
| `random` | `from`, an array of actions |

```json
{ "type": "sequence", "steps": [
    { "type": "obs", "command": "setScene", "scene": "Live" },
    { "type": "delay", "ms": 200 },
    { "type": "obs", "command": "startStream" }
] }
```

### When none of that fits

| Type | Parameters |
| --- | --- |
| `http` | `method`, `url`, `headers`, `body`, `saveResponseTo` |
| `ahk` | `script`. Runs AutoHotkey v2, which must be installed. |

---

## Conditions

`conditional` takes a small expression language. It is not a scripting language and cannot
call anything.

```
var.mode == "quiet"
var.cpu > 80 and not var.quiet
window.foregroundProcess == "chrome.exe" or var.forced
```

It has `==`, `!=`, `<`, `<=`, `>`, `>=`, and `and`, `or`, `not`, with brackets. Strings are
compared ignoring case, so `"Chrome.exe" == "chrome.exe"` holds. A name that has never been
set simply reads as unset rather than failing, so a condition never stops a button working.

Names come in two parts. `var.something` is a value a button remembered. `env.SOMETHING` is
a Windows environment variable. More sources arrive with the provider system.

---

## Adding a new action type

Add one file to `TouchDeck.Actions` and nothing else. The registry finds it by scanning the
assembly, the config validator starts accepting it, and the config center's picker shows it
with its own form built from what it declares.

```csharp
public sealed class BeepAction : IAction
{
    public string Type => "beep";
    public string Title => "Beep";
    public string Description => "Makes the computer beep.";

    public IReadOnlyList<ActionParameter> Parameters { get; } = new[]
    {
        ActionParameter.Optional("hz", ActionParameterKind.Number, "How high.", "800"),
    };

    public Task ExecuteAsync(ActionContext ctx, CancellationToken ct)
    {
        Console.Beep(ctx.Action.GetInt32("hz", 800), 200);
        return Task.CompletedTask;
    }
}
```

---

## Not working yet

These parse without complaint but do nothing, because the milestone that implements them has
not landed. They are in `MILESTONES.md`.

- `icon` on a button, and `backgroundImage` on a theme
- `releaseAction`, `longPressAction`, `doubleTapAction`, `repeat`, `confirm`
- `state`, `visibleWhen`, `enabledWhen`, and providers such as `system.cpu` or `obs.currentScene`
- `autoSwitch` on a profile, and swiping between pages
- `transition` on a theme
- `behaviour`: `startWithWindows`, `startMinimisedToTray`, `preventDisplaySleep`,
  `dimAfterSeconds`, `dimOpacity`, `wakeOnTouch`, `longPressMs`, `doubleTapMs`
- `feedback.pressSound`
- The tray icon, a global show and hide hotkey, and the on screen error overlay

---

## When something goes wrong

**A button press lands in the wrong place.** The panel is built never to take focus, so this
is usually Windows sending touches to the wrong screen. Open Tablet PC Settings, press
Setup, choose Touch input, and press Enter until the prompt appears on the touchscreen, then
touch it.

**A config file is broken.** The deck keeps running exactly as it was, on the last
configuration that loaded. The log says which file, which JSON path and which line.

**Nothing is on the touchscreen.** The log says which monitor it chose and why. If the one
you wanted was not attached, it falls back to `fallbackToIndex`.

**The log.** `%APPDATA%\TouchDeck\logs\touchdeck-<date>.log`. Set `logging.level` to `debug`
to see every action as it runs.

---

## Building it

```
dotnet build          # everything
dotnet test           # 195 tests
```

| Project | Holds |
| --- | --- |
| `TouchDeck.App` | The panel, the config center, startup. No action logic, no P/Invoke. |
| `TouchDeck.Core` | Config models, loading and validation, the action contract, expressions. |
| `TouchDeck.Actions` | One file per action type. |
| `TouchDeck.Platform` | Every P/Invoke: input injection, monitors, windows, audio, OBS. |
| `TouchDeck.Tests` | xUnit. |

`TESTING.md` covers the things only a real desktop can check, such as proving a press does
not steal focus.
