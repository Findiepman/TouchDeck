using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using TouchDeck.Core.Input;

namespace TouchDeck.App.Configurator;

/// <summary>
/// Records a key combination by having you press it, rather than asking you to spell it.
/// Nobody should have to know that the middle key is called "oem_period".
/// </summary>
public sealed class HotkeyCaptureBox : Border
{
    /// <summary>The combination, written the way config files write it.</summary>
    public static readonly DependencyProperty ComboProperty = DependencyProperty.Register(
        nameof(Combo),
        typeof(string),
        typeof(HotkeyCaptureBox),
        new FrameworkPropertyMetadata(
            null,
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            OnComboChanged,
            null,
            true,
            System.Windows.Data.UpdateSourceTrigger.PropertyChanged));

    private readonly TextBlock _text = new();
    private bool _capturing;

    /// <summary>Creates the capture box.</summary>
    public HotkeyCaptureBox()
    {
        Focusable = true;
        Cursor = Cursors.Hand;
        MinHeight = 36;
        Padding = new Thickness(11, 7, 11, 7);
        CornerRadius = new CornerRadius(4);
        BorderThickness = new Thickness(1);

        _text.VerticalAlignment = VerticalAlignment.Center;
        _text.FontSize = 13;
        Child = _text;

        Loaded += (_, _) => Paint();
        PreviewMouseLeftButtonDown += (_, e) =>
        {
            Focus();
            StartCapture();
            e.Handled = true;
        };
    }

    /// <summary>The combination, written the way config files write it.</summary>
    public string? Combo
    {
        get => (string?)GetValue(ComboProperty);
        set => SetValue(ComboProperty, value);
    }

    /// <inheritdoc />
    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);

        if (!_capturing)
        {
            // Space and Enter start a capture, so the box works without a mouse.
            if (e.Key is Key.Space or Key.Enter)
            {
                StartCapture();
                e.Handled = true;
            }

            return;
        }

        e.Handled = true;

        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        if (key == Key.Escape)
        {
            StopCapture();
            return;
        }

        if (IsModifier(key))
        {
            Paint();
            return;
        }

        Combo = Describe(key, Keyboard.Modifiers);
        StopCapture();
    }

    /// <inheritdoc />
    protected override void OnPreviewKeyUp(KeyEventArgs e)
    {
        base.OnPreviewKeyUp(e);

        if (_capturing)
        {
            e.Handled = true;
            Paint();
        }
    }

    /// <inheritdoc />
    protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {
        base.OnLostKeyboardFocus(e);
        StopCapture();
    }

    private static void OnComboChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((HotkeyCaptureBox)d).Paint();

    /// <summary>
    /// Turns a key press into the combination as config writes it. Kept separate from the
    /// control so that what is recorded can be checked against what the deck will send.
    /// </summary>
    /// <param name="key">The key that finished the combination.</param>
    /// <param name="modifiers">The modifiers held at that moment.</param>
    public static string Describe(Key key, ModifierKeys modifiers)
    {
        var virtualKey = (VirtualKey)KeyInterop.VirtualKeyFromKey(key);
        return string.Join("+", NamesOf(modifiers).Append(HotkeyParser.NameOf(virtualKey)));
    }

    private static bool IsModifier(Key key) => key
        is Key.LeftCtrl or Key.RightCtrl
        or Key.LeftShift or Key.RightShift
        or Key.LeftAlt or Key.RightAlt
        or Key.LWin or Key.RWin
        or Key.System;

    private static IEnumerable<string> ModifierNames() => NamesOf(Keyboard.Modifiers);

    private static IEnumerable<string> NamesOf(ModifierKeys modifiers)
    {
        if (modifiers.HasFlag(ModifierKeys.Control))
        {
            yield return "ctrl";
        }

        if (modifiers.HasFlag(ModifierKeys.Alt))
        {
            yield return "alt";
        }

        if (modifiers.HasFlag(ModifierKeys.Shift))
        {
            yield return "shift";
        }

        if (modifiers.HasFlag(ModifierKeys.Windows))
        {
            yield return "win";
        }
    }

    private void StartCapture()
    {
        _capturing = true;
        Paint();
    }

    private void StopCapture()
    {
        _capturing = false;
        Paint();
    }

    private void Paint()
    {
        if (!IsLoaded && _text.Parent is null)
        {
            return;
        }

        var signal = Brush("Signal", Colors.DeepPink);
        var field = Brush("Field", Color.FromRgb(0x1A, 0x19, 0x17));
        var edge = Brush("Edge", Color.FromRgb(0x3A, 0x37, 0x35));
        var ink = Brush("Ink", Colors.White);
        var dim = Brush("InkDim", Colors.Gray);

        Background = field;

        if (_capturing)
        {
            BorderBrush = signal;
            _text.Foreground = signal;

            var held = ModifierNames().ToList();
            _text.Text = held.Count == 0
                ? "Press the keys you want to send"
                : string.Join("+", held) + "+ …";
            return;
        }

        BorderBrush = IsKeyboardFocused ? signal : edge;

        if (string.IsNullOrWhiteSpace(Combo))
        {
            _text.Foreground = dim;
            _text.Text = "Click, then press the keys";
        }
        else
        {
            _text.Foreground = ink;
            _text.Text = Combo;
        }
    }

    private Brush Brush(string key, Color fallback) =>
        TryFindResource(key) as Brush ?? new SolidColorBrush(fallback);
}
