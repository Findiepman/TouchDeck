using System.Windows.Input;
using TouchDeck.App.Configurator;
using TouchDeck.Core.Input;
using Xunit;

namespace TouchDeck.Tests;

/// <summary>
/// What the config center records when you press a combination has to be exactly what the
/// deck later sends, so these go the whole way round: key press, written combo, parsed keys.
/// </summary>
public class HotkeyCaptureTests
{
    [Theory]
    [InlineData(Key.K, ModifierKeys.Control | ModifierKeys.Shift, "ctrl+shift+k")]
    [InlineData(Key.C, ModifierKeys.Control, "ctrl+c")]
    [InlineData(Key.F13, ModifierKeys.None, "f13")]
    [InlineData(Key.Left, ModifierKeys.Alt, "alt+left")]
    [InlineData(Key.S, ModifierKeys.Control | ModifierKeys.Shift | ModifierKeys.Alt, "ctrl+alt+shift+s")]
    [InlineData(Key.L, ModifierKeys.Windows, "win+l")]
    [InlineData(Key.Delete, ModifierKeys.Control | ModifierKeys.Alt, "ctrl+alt+delete")]
    [InlineData(Key.D4, ModifierKeys.Control, "ctrl+4")]
    [InlineData(Key.Escape, ModifierKeys.Control | ModifierKeys.Shift, "ctrl+shift+esc")]
    public void APressIsWrittenTheWayConfigWritesIt(Key key, ModifierKeys modifiers, string expected) =>
        Assert.Equal(expected, HotkeyCaptureBox.Describe(key, modifiers));

    [Theory]
    [InlineData(Key.K, ModifierKeys.Control | ModifierKeys.Shift)]
    [InlineData(Key.F5, ModifierKeys.None)]
    [InlineData(Key.Right, ModifierKeys.Control)]
    [InlineData(Key.OemPeriod, ModifierKeys.Alt)]
    [InlineData(Key.NumPad7, ModifierKeys.None)]
    public void WhatIsRecordedIsWhatTheDeckWillSend(Key key, ModifierKeys modifiers)
    {
        var written = HotkeyCaptureBox.Describe(key, modifiers);

        Assert.True(HotkeyParser.TryParse(written, out var combo, out var error), error);
        Assert.Equal((VirtualKey)KeyInterop.VirtualKeyFromKey(key), combo!.Key);
        Assert.Equal(
            modifiers.HasFlag(ModifierKeys.Control),
            combo.Modifiers.Contains(VirtualKey.LeftControl));
        Assert.Equal(
            modifiers.HasFlag(ModifierKeys.Shift),
            combo.Modifiers.Contains(VirtualKey.LeftShift));
        Assert.Equal(
            modifiers.HasFlag(ModifierKeys.Alt),
            combo.Modifiers.Contains(VirtualKey.LeftAlt));
        Assert.Equal(
            modifiers.HasFlag(ModifierKeys.Windows),
            combo.Modifiers.Contains(VirtualKey.LeftWindows));
    }

    [Fact]
    public void ModifiersAreAlwaysWrittenInTheSameOrder() =>
        Assert.Equal(
            "ctrl+alt+shift+win+p",
            HotkeyCaptureBox.Describe(
                Key.P,
                ModifierKeys.Shift | ModifierKeys.Windows | ModifierKeys.Control | ModifierKeys.Alt));
}
