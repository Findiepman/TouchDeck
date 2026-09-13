namespace TouchDeck.Core.Configuration;

/// <summary>
/// Writes a commented, working configuration the first time the app runs, so there is
/// always something on screen and always an example to copy from.
/// </summary>
public static class StarterConfig
{
    /// <summary>Creates any starter file that is not already there.</summary>
    /// <param name="paths">Where the files go.</param>
    /// <returns>The files that were created, which is empty on every run after the first.</returns>
    public static IReadOnlyList<string> EnsureExists(ConfigPaths paths)
    {
        paths.EnsureDirectories();

        var created = new List<string>();
        WriteIfMissing(paths.ConfigFile, ConfigJsonText, created);
        WriteIfMissing(paths.ThemesFile, ThemesJsonText, created);
        WriteIfMissing(Path.Combine(paths.ProfilesDirectory, "default.json"), DefaultProfileText, created);
        WriteIfMissing(Path.Combine(paths.ProfilesDirectory, "media.json"), MediaProfileText, created);
        return created;
    }

    private static void WriteIfMissing(string path, string contents, List<string> created)
    {
        if (File.Exists(path))
        {
            return;
        }

        File.WriteAllText(path, contents);
        created.Add(path);
    }

    private const string ConfigJsonText = """
    {
      // TouchDeck global settings. Save this file and the deck reloads itself.
      // Anything you leave out falls back to a sensible default.
      "$schema": "./touchdeck.schema.json",
      "version": 1,

      "display": {
        // How to pick the monitor the deck lives on.
        //   "byName"       use the Windows device name in "value", e.g. "\\\\.\\DISPLAY2"
        //   "byIndex"      use the number in "value". 0 is always the primary monitor,
        //                  the rest follow left to right, top to bottom
        //   "byResolution" use "WIDTHxHEIGHT" in "value", e.g. "1024x600"
        //   "primary"      whichever monitor Windows calls primary
        "select": "byIndex",
        "value": "1",

        // Used when nothing matches, so a replugged touchscreen never leaves you blank.
        "fallbackToIndex": 0,

        // Cover the whole monitor. Set false and fill in "bounds" to use part of it.
        "fullscreen": true,
        "bounds": null
      },

      "behaviour": {
        "startWithWindows": false,
        "startMinimisedToTray": false,
        "preventDisplaySleep": true,
        "singleInstance": true,

        // Profile shown at startup, matched against the "id" in profiles/*.json.
        "defaultProfile": "default",

        // Dim the panel after this many seconds of no touches. 0 turns dimming off.
        "dimAfterSeconds": 300,
        "dimOpacity": 0.25,
        "wakeOnTouch": true,

        // Press timings, in milliseconds.
        "longPressMs": 500,
        "doubleTapMs": 250,

        // Reload this folder whenever a file in it is saved.
        "hotReload": true,

        // An action that runs longer than this is cancelled and logged.
        "actionTimeoutMs": 10000
      },

      "feedback": {
        // Path to a wav file played on every press, or null for silence.
        "pressSound": null,
        "pressSoundVolume": 0.5
      },

      "integrations": {
        "obs": {
          "enabled": false,
          "host": "localhost",
          "port": 4455,
          "password": "",
          "autoReconnect": true
        }
      },

      "logging": {
        // verbose, debug, information, warning, error or fatal.
        "level": "information",
        "retainDays": 7
      }
    }

    """;

    private const string ThemesJsonText = """
    {
      // Named themes. A profile picks one with "theme": "<name>", and any button can
      // override any single value here with its own "style" block.
      "$schema": "./touchdeck.schema.json",
      "themes": {

        "dark": {
          "background": "#0B0D10",
          "backgroundImage": null,

          // Space between cells, and between the grid and the screen edge.
          "gap": 8,
          "padding": 12,

          "button": {
            "background": "#171A1F",
            "backgroundPressed": "#252A32",
            "backgroundDisabled": "#101216",
            "border": "#22262D",
            "borderWidth": 1,
            "cornerRadius": 12,
            "textColour": "#E8EAED",
            "textColourPressed": "#FFFFFF",
            "fontFamily": "Segoe UI Variable Display, Segoe UI",
            "fontSize": 13,
            "fontWeight": "SemiBold",

            // top, bottom, center or none.
            "labelPosition": "center",
            "iconSize": 40,
            "iconColour": "#CFD4DC",
            "padding": 8
          },

          // What a press looks like. This is the only feedback a touchscreen gives.
          "press": {
            "scale": 0.94,
            "durationMs": 70,
            "easing": "cubicOut"
          },

          "transition": {
            "pageChangeMs": 140,
            "style": "fade"
          }
        },

        // Themes can inherit, so a variant only states what differs.
        "light": {
          "inherits": "dark",
          "background": "#F2F3F5",
          "button": {
            "background": "#FFFFFF",
            "backgroundPressed": "#E4E7EB",
            "border": "#D6DAE0",
            "textColour": "#12151A",
            "textColourPressed": "#000000",
            "iconColour": "#3B4150"
          }
        }
      }
    }

    """;

    private const string DefaultProfileText = """
    {
      // A profile is one grid, one theme and one or more pages of buttons.
      "$schema": "../touchdeck.schema.json",
      "id": "default",
      "name": "Default",
      "theme": "dark",

      "grid": {
        "columns": 5,
        "rows": 3

        // Cells are square by default. Set "cellAspect" for a different shape,
        // or "fill": true to stretch them to the whole panel.
      },

      "pages": [
        {
          "id": "main",
          "buttons": [
            // An icon of type glyph is a Segoe Fluent Icons code point. Nothing to install.
            { "col": 0, "row": 0, "label": "Copy",  "icon": { "type": "glyph", "value": "E8C8" }, "action": { "type": "hotkey", "keys": "ctrl+c" } },
            { "col": 1, "row": 0, "label": "Paste", "icon": { "type": "glyph", "value": "E77F" }, "action": { "type": "hotkey", "keys": "ctrl+v" } },
            { "col": 2, "row": 0, "label": "Undo",  "icon": { "type": "glyph", "value": "E7A7" }, "action": { "type": "hotkey", "keys": "ctrl+z" } },
            { "col": 3, "row": 0, "label": "Redo",  "icon": { "type": "glyph", "value": "E7A6" }, "action": { "type": "hotkey", "keys": "ctrl+y" } },
            { "col": 4, "row": 0, "label": "Save",  "icon": { "type": "glyph", "value": "E74E" }, "action": { "type": "hotkey", "keys": "ctrl+s" } },

            // Icons are optional, and a button without one is still just its label.
            { "col": 0, "row": 1, "label": "Select All", "action": { "type": "hotkey", "keys": "ctrl+a" } },
            { "col": 1, "row": 1, "label": "Find",       "icon": { "type": "glyph", "value": "E721" }, "action": { "type": "hotkey", "keys": "ctrl+f" } },
            { "col": 2, "row": 1, "label": "Snip",       "action": { "type": "hotkey", "keys": "win+shift+s" } },
            { "col": 3, "row": 1, "label": "Task Mgr",   "action": { "type": "hotkey", "keys": "ctrl+shift+esc" } },

            // Any button can override the theme for itself.
            {
              "col": 4, "row": 1,
              "label": "Lock PC",
              "icon": { "type": "glyph", "value": "E72E" },
              "style": { "background": "#5A1D1D", "textColour": "#FFD9D9" },
              "action": { "type": "hotkey", "keys": "win+l" }
            },

            { "col": 0, "row": 2, "label": "Notepad",  "icon": { "type": "glyph", "value": "E70F" }, "action": { "type": "launch", "path": "notepad.exe" } },
            { "col": 1, "row": 2, "label": "Calc",     "icon": { "type": "glyph", "value": "E8EF" }, "action": { "type": "launch", "path": "calc.exe" } },
            { "col": 2, "row": 2, "label": "Explorer", "icon": { "type": "glyph", "value": "E8B7" }, "action": { "type": "launch", "path": "explorer.exe" } },

            // Bring the terminal forward if it is already open, rather than opening another.
            {
              "col": 3, "row": 2,
              "label": "Terminal",
              "icon": { "type": "glyph", "value": "E756" },
              "action": { "type": "launch", "path": "powershell.exe", "focusIfRunning": true }
            },

            // A folder is a page you reach from a button, with a way back on it.
            { "col": 4, "row": 2, "label": "More", "icon": { "type": "glyph", "value": "E712" }, "action": { "type": "openFolder", "page": "more" } }
          ]
        },

        {
          "id": "more",
          "name": "More",
          "isFolder": true,

          // Where the way back sits, and what it says.
          "backButton": { "col": 4, "row": 2, "label": "Back", "icon": { "type": "glyph", "value": "E72B" }, "action": { "type": "closeFolder" } },

          "buttons": [
            // Types the text wherever the caret is. Any character works, including emoji.
            {
              "col": 0, "row": 0,
              "label": "Signature",
              "action": { "type": "text", "value": "Sent from my deck" }
            },

            // Puts something on the clipboard without going near the keyboard.
            {
              "col": 1, "row": 0,
              "label": "Copy link",
              "action": { "type": "clipboard", "operation": "set", "value": "https://example.com" }
            },

            { "col": 2, "row": 0, "label": "Open site",
              "action": { "type": "open", "url": "https://example.com" } },

            // Several things in a row, with a pause so the first one lands.
            {
              "col": 3, "row": 0,
              "label": "Tidy up",
              "action": {
                "type": "sequence",
                "steps": [
                  { "type": "window", "operation": "minimise", "processName": "explorer.exe" },
                  { "type": "delay", "ms": 150 },
                  { "type": "setVariable", "name": "tidied", "value": "true", "scope": "session" }
                ]
              }
            },

            { "col": 0, "row": 1, "label": "Volume -",
              "action": { "type": "audio", "operation": "adjust", "value": -0.1 } },
            { "col": 1, "row": 1, "label": "Mute",
              "action": { "type": "audio", "operation": "toggleMute" } },
            { "col": 2, "row": 1, "label": "Volume +",
              "action": { "type": "audio", "operation": "adjust", "value": 0.1 } },

            // Remembers a value, which a condition elsewhere can test.
            { "col": 3, "row": 1, "label": "Flip mode",
              "action": { "type": "toggleVariable", "name": "quiet" } },

            // Does one thing or the other depending on what is remembered.
            {
              "col": 0, "row": 2,
              "label": "Depends",
              "action": {
                "type": "conditional",
                "if": "var.quiet == true",
                "then": { "type": "audio", "operation": "mute" },
                "else": { "type": "audio", "operation": "unmute" }
              }
            },

            { "col": 1, "row": 2, "label": "Media",   "action": { "type": "switchProfile", "profile": "media" } }
          ]
        }
      ]
    }

    """;

    private const string MediaProfileText = """
    {
      // A second profile, to show that grid size and theme are per profile.
      "$schema": "../touchdeck.schema.json",
      "id": "media",
      "name": "Media",
      "theme": "light",

      "grid": { "columns": 3, "rows": 2 },

      "pages": [
        {
          "id": "main",
          "buttons": [
            // These go through the media keys, so whatever is playing picks them up.
            { "col": 0, "row": 0, "label": "Previous", "icon": { "type": "glyph", "value": "E892" }, "action": { "type": "media", "command": "previous" } },
            { "col": 1, "row": 0, "label": "Play",     "icon": { "type": "glyph", "value": "E768" }, "action": { "type": "media", "command": "playPause" } },
            { "col": 2, "row": 0, "label": "Next",     "icon": { "type": "glyph", "value": "E893" }, "action": { "type": "media", "command": "next" } },

            // These change the Windows volume itself rather than pressing a key. An icon of
            // type text is literal characters drawn at icon size, for when no glyph fits.
            {
              "col": 0, "row": 1,
              "label": "Volume -",
              "icon": { "type": "text", "value": "−" },
              "action": { "type": "audio", "operation": "adjust", "value": -0.05 }
            },
            { "col": 1, "row": 1, "label": "Mute",     "icon": { "type": "glyph", "value": "E74F" }, "action": { "type": "audio", "operation": "toggleMute" } },
            { "col": 2, "row": 1, "label": "Back",     "icon": { "type": "glyph", "value": "E72B" }, "action": { "type": "switchProfile", "profile": "default" } }
          ]
        }
      ]
    }

    """;
}
