namespace TouchDeck.Core.Input;

/// <summary>
/// A parsed key combination. Modifiers are pressed in order and released in reverse, which
/// is the only ordering applications reliably accept.
/// </summary>
/// <param name="Modifiers">Modifier keys held while the main key is pressed.</param>
/// <param name="Key">The main key, or null for a modifier only combo such as <c>ctrl</c>.</param>
public sealed record KeyCombo(IReadOnlyList<VirtualKey> Modifiers, VirtualKey? Key)
{
    /// <summary>An empty combo, which sends nothing.</summary>
    public static KeyCombo Empty { get; } = new(Array.Empty<VirtualKey>(), null);

    /// <summary>True when the combo would send no keys at all.</summary>
    public bool IsEmpty => Modifiers.Count == 0 && Key is null;

    /// <summary>Keys in the order they must be pressed.</summary>
    public IReadOnlyList<VirtualKey> PressOrder
    {
        get
        {
            var keys = new List<VirtualKey>(Modifiers.Count + 1);
            keys.AddRange(Modifiers);
            if (Key is { } key)
            {
                keys.Add(key);
            }

            return keys;
        }
    }

    /// <summary>Keys in the order they must be released.</summary>
    public IReadOnlyList<VirtualKey> ReleaseOrder
    {
        get
        {
            var keys = PressOrder.ToList();
            keys.Reverse();
            return keys;
        }
    }

    /// <summary>Renders the combo the way it would be written in config.</summary>
    public override string ToString() => string.Join("+", PressOrder.Select(HotkeyParser.NameOf));
}
