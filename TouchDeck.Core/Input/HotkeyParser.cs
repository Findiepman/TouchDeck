using System.Diagnostics.CodeAnalysis;

namespace TouchDeck.Core.Input;

/// <summary>
/// Turns a written combo such as <c>ctrl+shift+m</c> into a <see cref="KeyCombo"/>.
/// Parsing is deliberately forgiving about case, spacing and alias spellings, because this
/// is the single string most users will type by hand.
/// </summary>
public static class HotkeyParser
{
    private static readonly IReadOnlyDictionary<string, VirtualKey> Modifiers =
        new Dictionary<string, VirtualKey>(StringComparer.OrdinalIgnoreCase)
        {
            ["ctrl"] = VirtualKey.LeftControl,
            ["control"] = VirtualKey.LeftControl,
            ["lctrl"] = VirtualKey.LeftControl,
            ["leftctrl"] = VirtualKey.LeftControl,
            ["rctrl"] = VirtualKey.RightControl,
            ["rightctrl"] = VirtualKey.RightControl,
            ["shift"] = VirtualKey.LeftShift,
            ["lshift"] = VirtualKey.LeftShift,
            ["leftshift"] = VirtualKey.LeftShift,
            ["rshift"] = VirtualKey.RightShift,
            ["rightshift"] = VirtualKey.RightShift,
            ["alt"] = VirtualKey.LeftAlt,
            ["lalt"] = VirtualKey.LeftAlt,
            ["leftalt"] = VirtualKey.LeftAlt,
            ["ralt"] = VirtualKey.RightAlt,
            ["rightalt"] = VirtualKey.RightAlt,
            ["altgr"] = VirtualKey.RightAlt,
            ["win"] = VirtualKey.LeftWindows,
            ["lwin"] = VirtualKey.LeftWindows,
            ["rwin"] = VirtualKey.RightWindows,
            ["super"] = VirtualKey.LeftWindows,
            ["meta"] = VirtualKey.LeftWindows,
            ["cmd"] = VirtualKey.LeftWindows,
        };

    private static readonly IReadOnlyDictionary<string, VirtualKey> Keys = BuildKeyTable();

    private static readonly IReadOnlyDictionary<VirtualKey, string> CanonicalNames = BuildNameTable();

    /// <summary>Every accepted key name, for documentation and error messages.</summary>
    public static IReadOnlyCollection<string> KnownNames =>
        Modifiers.Keys.Concat(Keys.Keys).OrderBy(k => k, StringComparer.Ordinal).ToArray();

    /// <summary>Parses a combo, throwing <see cref="FormatException"/> when it cannot.</summary>
    public static KeyCombo Parse(string text) =>
        TryParse(text, out var combo, out var error) ? combo : throw new FormatException(error);

    /// <summary>Parses a combo, reporting why it failed instead of throwing.</summary>
    /// <param name="text">The combo, for example <c>ctrl+shift+m</c>.</param>
    /// <param name="combo">The parsed combo when parsing succeeds.</param>
    /// <param name="error">A message suitable for showing the user when parsing fails.</param>
    public static bool TryParse(
        string? text,
        [NotNullWhen(true)] out KeyCombo? combo,
        [NotNullWhen(false)] out string? error)
    {
        combo = null;
        error = null;

        if (string.IsNullOrWhiteSpace(text))
        {
            error = "The key combination is empty.";
            return false;
        }

        var modifiers = new List<VirtualKey>();
        VirtualKey? mainKey = null;

        foreach (var token in Tokenise(text))
        {
            if (Modifiers.TryGetValue(token, out var modifier))
            {
                if (!modifiers.Contains(modifier))
                {
                    modifiers.Add(modifier);
                }

                continue;
            }

            if (!Keys.TryGetValue(token, out var key))
            {
                error = $"\"{token}\" is not a key name.";
                return false;
            }

            if (mainKey is not null)
            {
                error = $"A combination can only have one non modifier key, but it has \"{NameOf(mainKey.Value)}\" and \"{token}\".";
                return false;
            }

            mainKey = key;
        }

        if (modifiers.Count == 0 && mainKey is null)
        {
            error = "The key combination is empty.";
            return false;
        }

        combo = new KeyCombo(modifiers, mainKey);
        return true;
    }

    /// <summary>Returns the canonical written name of a key.</summary>
    public static string NameOf(VirtualKey key) =>
        CanonicalNames.TryGetValue(key, out var name) ? name : key.ToString().ToLowerInvariant();

    /// <summary>
    /// Splits on <c>+</c>, except where <c>+</c> is itself the key: <c>ctrl++</c> is control
    /// plus the plus key.
    /// </summary>
    private static IEnumerable<string> Tokenise(string text)
    {
        var buffer = new System.Text.StringBuilder();

        foreach (var c in text)
        {
            if (char.IsWhiteSpace(c))
            {
                continue;
            }

            if (c == '+' && buffer.Length > 0)
            {
                yield return buffer.ToString();
                buffer.Clear();
                continue;
            }

            buffer.Append(c);
        }

        if (buffer.Length > 0)
        {
            yield return buffer.ToString();
        }
    }

    private static Dictionary<string, VirtualKey> BuildKeyTable()
    {
        var table = new Dictionary<string, VirtualKey>(StringComparer.OrdinalIgnoreCase);

        for (var c = 'a'; c <= 'z'; c++)
        {
            table[c.ToString()] = VirtualKey.A + (c - 'a');
        }

        for (var d = 0; d <= 9; d++)
        {
            table[d.ToString()] = VirtualKey.D0 + d;
            table["num" + d] = VirtualKey.NumPad0 + d;
            table["numpad" + d] = VirtualKey.NumPad0 + d;
        }

        for (var f = 1; f <= 24; f++)
        {
            table["f" + f] = VirtualKey.F1 + (f - 1);
        }

        var named = new (string Name, VirtualKey Key)[]
        {
            ("backspace", VirtualKey.Backspace),
            ("back", VirtualKey.Backspace),
            ("tab", VirtualKey.Tab),
            ("clear", VirtualKey.Clear),
            ("enter", VirtualKey.Enter),
            ("return", VirtualKey.Enter),
            ("pause", VirtualKey.Pause),
            ("capslock", VirtualKey.CapsLock),
            ("esc", VirtualKey.Escape),
            ("escape", VirtualKey.Escape),
            ("space", VirtualKey.Space),
            ("spacebar", VirtualKey.Space),
            ("pageup", VirtualKey.PageUp),
            ("pgup", VirtualKey.PageUp),
            ("pagedown", VirtualKey.PageDown),
            ("pgdn", VirtualKey.PageDown),
            ("end", VirtualKey.End),
            ("home", VirtualKey.Home),
            ("left", VirtualKey.Left),
            ("up", VirtualKey.Up),
            ("right", VirtualKey.Right),
            ("down", VirtualKey.Down),
            ("printscreen", VirtualKey.PrintScreen),
            ("prtsc", VirtualKey.PrintScreen),
            ("insert", VirtualKey.Insert),
            ("ins", VirtualKey.Insert),
            ("delete", VirtualKey.Delete),
            ("del", VirtualKey.Delete),
            ("apps", VirtualKey.Applications),
            ("menu", VirtualKey.Applications),
            ("numlock", VirtualKey.NumLock),
            ("scrolllock", VirtualKey.ScrollLock),
            ("numpadmultiply", VirtualKey.Multiply),
            ("numpadadd", VirtualKey.Add),
            ("numpadsubtract", VirtualKey.Subtract),
            ("numpaddecimal", VirtualKey.Decimal),
            ("numpaddivide", VirtualKey.Divide),
            ("numpadseparator", VirtualKey.Separator),
            ("volumemute", VirtualKey.VolumeMute),
            ("volumedown", VirtualKey.VolumeDown),
            ("volumeup", VirtualKey.VolumeUp),
            ("medianext", VirtualKey.MediaNextTrack),
            ("mediaprevious", VirtualKey.MediaPreviousTrack),
            ("mediastop", VirtualKey.MediaStop),
            ("mediaplaypause", VirtualKey.MediaPlayPause),
            ("browserback", VirtualKey.BrowserBack),
            ("browserforward", VirtualKey.BrowserForward),
            ("browserrefresh", VirtualKey.BrowserRefresh),
            ("browserstop", VirtualKey.BrowserStop),
            ("browsersearch", VirtualKey.BrowserSearch),
            ("browserfavorites", VirtualKey.BrowserFavorites),
            ("browserhome", VirtualKey.BrowserHome),
            (";", VirtualKey.Semicolon),
            ("semicolon", VirtualKey.Semicolon),
            ("+", VirtualKey.Plus),
            ("=", VirtualKey.Plus),
            ("plus", VirtualKey.Plus),
            ("equals", VirtualKey.Plus),
            (",", VirtualKey.Comma),
            ("comma", VirtualKey.Comma),
            ("-", VirtualKey.Minus),
            ("minus", VirtualKey.Minus),
            (".", VirtualKey.Period),
            ("period", VirtualKey.Period),
            ("/", VirtualKey.Slash),
            ("slash", VirtualKey.Slash),
            ("`", VirtualKey.Grave),
            ("grave", VirtualKey.Grave),
            ("tilde", VirtualKey.Grave),
            ("[", VirtualKey.OpenBracket),
            ("openbracket", VirtualKey.OpenBracket),
            ("\\", VirtualKey.Backslash),
            ("backslash", VirtualKey.Backslash),
            ("]", VirtualKey.CloseBracket),
            ("closebracket", VirtualKey.CloseBracket),
            ("'", VirtualKey.Quote),
            ("quote", VirtualKey.Quote),
        };

        foreach (var (name, key) in named)
        {
            table[name] = key;
        }

        return table;
    }

    private static Dictionary<VirtualKey, string> BuildNameTable()
    {
        var names = new Dictionary<VirtualKey, string>
        {
            [VirtualKey.LeftControl] = "ctrl",
            [VirtualKey.RightControl] = "rctrl",
            [VirtualKey.LeftShift] = "shift",
            [VirtualKey.RightShift] = "rshift",
            [VirtualKey.LeftAlt] = "alt",
            [VirtualKey.RightAlt] = "ralt",
            [VirtualKey.LeftWindows] = "win",
            [VirtualKey.RightWindows] = "rwin",
        };

        foreach (var (name, key) in Keys)
        {
            // The table is built alias first, so only the first spelling of each key sticks.
            if (!names.ContainsKey(key) && name.All(char.IsLetterOrDigit))
            {
                names[key] = name;
            }
        }

        return names;
    }
}
