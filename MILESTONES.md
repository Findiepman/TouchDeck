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

A twenty seventh arrived later and is not in the brief at all: `soundboard`, which plays
clips QuoteDeck has already rendered, drawn from a shuffle bag and played to as many output
devices at once as you name.

Three things the brief puts later came with it, because the actions would otherwise be
hollow: a deck controller that really changes page and profile, a variable store that
remembers across restarts, and the expression language.

## M3 — Structure

What is left after M2 did the switching: automatic profile switching from the foreground
window, using a window event hook rather than polling; swipe between pages with inertia; and
the page change animation. Pages, folders, back buttons and manual switching are done.

## M4 — Icons and looks — done apart from svg

Buttons stopped being text on a rectangle.

- `icon` with three of the four kinds the brief names: an image file, a Segoe Fluent Icons
  glyph, literal text, or none — done
- Icon size, colour and tinting, and the icons folder under the config root — done
- Label and icon together: `labelPosition` of top, bottom, center or none — done
- `backgroundImage` on a theme — done
- An icon picker in the config center, so choosing one does not mean typing a path — done,
  a file dialog that copies into the icons folder and a grid of common glyphs
- Svg — **not done**, see below

The svg question was answered by splitting it off rather than deciding it up front. Png,
glyphs and text cover the ground without a new dependency, and svg is now a small isolated
job behind `IconFactory`: an svg is recognised, refused and explained rather than silently
drawing nothing. If it turns out to be wanted, SharpVectors is the choice — pure managed,
converts to a WPF `DrawingImage`, so it composes with the existing tinting. Svg.Skia drags
native binaries in and would make M6's single file publish harder.

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
