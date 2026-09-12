using Serilog.Core;
using TouchDeck.Actions;
using TouchDeck.Core.Actions;

namespace TouchDeck.Tests;

/// <summary>
/// The action types that really exist, so config tests do not have to keep a list of their
/// own in step with the ones that ship.
/// </summary>
public static class KnownActions
{
    /// <summary>Every action type the deck can run.</summary>
    public static IReadOnlyCollection<string> Types { get; } =
        ActionRegistry.Scan(Logger.None, typeof(HotkeyAction).Assembly).KnownTypes;
}
