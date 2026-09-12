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

    /// <summary>Types literal text, including characters no key on the keyboard produces.</summary>
    /// <param name="text">What to type.</param>
    void TypeText(string text);

    /// <summary>Presses, holds or releases a mouse button where the pointer is.</summary>
    /// <param name="button">Which button.</param>
    /// <param name="action">Whether to click, hold or release.</param>
    void MouseButton(MouseButton button, PressAction action);

    /// <summary>Moves the pointer.</summary>
    /// <param name="x">Horizontal position, or offset when relative.</param>
    /// <param name="y">Vertical position, or offset when relative.</param>
    /// <param name="relative">True to move by the amount rather than to the position.</param>
    void MoveMouse(int x, int y, bool relative);

    /// <summary>Turns the scroll wheel.</summary>
    /// <param name="amount">How many notches.</param>
    /// <param name="direction">Which way.</param>
    void Scroll(int amount, ScrollDirection direction);
}
