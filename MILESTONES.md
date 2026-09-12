# TouchDeck milestones

Where the build is, and what is left. Things that landed out of order are listed under the
milestone that asked for them, marked done.

## M1 — It works at all — done

Window on the chosen monitor, borderless, topmost, never taking focus, per monitor DPI.
Grid rendered from config. Hot reload. A broken config leaves the last good one running.
`hotkey` and `launch`.

Pulled forward from later milestones: the action interface, the assembly scanned registry,
the action context, per action timeouts and error isolation (M2); themes and per button
style overrides (M5).

## M2 — The action system — done

All 26 action types from section 6.2:

- Input: `hotkey`, `keyDown`, `keyUp`, `text`, `mouse`, `scroll`
- Apps and system: `launch` with its focus options, `open`, `shell`, `window`, `clipboard`
- Audio: `audio`, `media`
- OBS: `obs`
- Deck control: `switchProfile`, `switchPage`, `openFolder`, `closeFolder`, `setVariable`,
  `toggleVariable`
- Composition: `sequence`, `delay`, `conditional`, `random`
- Escape hatches: `http`, `ahk`

Three things the brief puts later came with it, because the actions would otherwise be
hollow: a deck controller that really changes page and profile, a variable store that
remembers across restarts, and the expression language.

## M3 — Structure

What is left after M2 did the switching: automatic profile switching from the foreground
window, using a window event hook rather than polling; swipe between pages with inertia; and
the page change animation. Pages, folders, back buttons and manual switching are done.

## M4 — Icons and looks

Buttons stop being text on a rectangle.

- `icon` with the four kinds the brief names: a png or svg file, a Segoe Fluent Icons glyph,
  literal text, or none
- Icon size, colour and tinting, and the icons folder under the config root
- Label and icon together: `labelPosition` of top, bottom, center or none
- `backgroundImage` on a theme
- An icon picker in the config center, so choosing one does not mean typing a path

One thing to decide before starting: WPF cannot draw svg on its own. Doing it properly means
one more dependency, which the brief says to ask about first. The alternatives are to support
png only, or to rasterise svg at load. I will ask rather than choose.

## M5 — Live data

Themes and per button style overrides are done. What is left is everything that changes while
the deck is running:

- The provider system, and the providers the brief names: `system.cpu`, `system.ram`,
  `system.time`, `audio.volume`, `audio.muted`, `obs.currentScene`, `obs.streaming`,
  `obs.recording`, `obs.inputMuted`, `window.foregroundProcess`, `var.*`
- `state` binding, so a button looks different when the mic is muted
- `{{...}}` interpolation in labels, icon paths and action parameters
- `visibleWhen` and `enabledWhen` on top of the expression language from M2
- The remaining press behaviours: `repeat`, `confirm`, `longPressAction`, `releaseAction`,
  `doubleTapAction`

## M6 — Living with it

- Tray icon and menu, which is what finally replaces `--quit`
- A global hotkey to show and hide the panel
- Start with Windows — done, ahead of the rest of this milestone
- Idle dimming, and keeping the touchscreen awake
- The validation error overlay on the panel itself, instead of only in the log
- A generated JSON schema, so hand editing gets autocomplete
- Single file self contained publish
- A README documenting every config property

Two of these are arguably urgent rather than last: the tray icon, because closing the deck
needs a command line today, and the error overlay, because config mistakes are only visible
in the log.

## M7 — On-device editor — optional

Edit the deck from the touchscreen: an edit mode, long press an empty cell to add a button,
drag to rearrange. Its central idea, an action picker driven by registry metadata so it stays
in sync by itself, already exists in the config center.
