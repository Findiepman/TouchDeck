using TouchDeck.Core.Input;

namespace TouchDeck.Core.Abstractions;

/// <summary>
/// Sends keystrokes to whatever currently has focus. Implementations must use scan codes so
/// that games and anything on raw input actually receive them, and must never leave a
/// modifier stuck down.
/// </summary>
public interface IInputInjector
{
    /// <summary>Presses and releases a combo once.</summary>
    /// <param name="combo">The keys to send.</param>
    void SendCombo(KeyCombo combo);

    /// <summary>Presses a combo and leaves it held.</summary>
    /// <param name="combo">The keys to hold down.</param>
    void HoldCombo(KeyCombo combo);

    /// <summary>Releases a combo that <see cref="HoldCombo"/> is holding.</summary>
    /// <param name="combo">The keys to release.</param>
    void ReleaseCombo(KeyCombo combo);

    /// <summary>Releases every key this injector is currently holding down.</summary>
    void ReleaseAllHeldKeys();
}
