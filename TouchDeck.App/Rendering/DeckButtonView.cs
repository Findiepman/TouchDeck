using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using TouchDeck.Core.Configuration;

namespace TouchDeck.App.Rendering;

/// <summary>
/// One button on the panel. It fires on the way down rather than on click, because with no
/// haptics the only feedback a touchscreen gives is how quickly something happens.
/// </summary>
public sealed class DeckButtonView : Border
{
    private readonly ResolvedButtonStyle _style;
    private readonly ResolvedPressStyle _press;
    private readonly TextBlock _label;
    private readonly ScaleTransform _scale = new(1, 1);

    private bool _isPressed;

    /// <summary>Builds the visual for a button.</summary>
    /// <param name="config">The button as configured.</param>
    /// <param name="style">The style after theme and per button overrides are merged.</param>
    /// <param name="press">The press animation from the theme.</param>
    public DeckButtonView(ButtonConfig config, ResolvedButtonStyle style, ResolvedPressStyle press)
    {
        Config = config;
        _style = style;
        _press = press;

        Background = StyleTranslator.Brush(style.Background);
        BorderBrush = StyleTranslator.Brush(style.Border);
        BorderThickness = new Thickness(style.BorderWidth);
        CornerRadius = new CornerRadius(style.CornerRadius);
        Padding = new Thickness(style.Padding);
        SnapsToDevicePixels = true;

        RenderTransform = _scale;
        RenderTransformOrigin = new Point(0.5, 0.5);

        // Without these Windows waits to see whether a touch is a press and hold, and every
        // button feels like it is lagging.
        Stylus.SetIsPressAndHoldEnabled(this, false);
        Stylus.SetIsFlicksEnabled(this, false);
        Stylus.SetIsTapFeedbackEnabled(this, false);

        _label = new TextBlock
        {
            Text = config.Label ?? string.Empty,
            Foreground = StyleTranslator.Brush(style.TextColour),
            FontFamily = StyleTranslator.Font(style.FontFamily),
            FontSize = style.FontSize,
            FontWeight = StyleTranslator.Weight(style.FontWeight),
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = StyleTranslator.LabelAlignment(LabelPosition),
            IsHitTestVisible = false,
        };

        if (LabelPosition != Core.Configuration.LabelPosition.None)
        {
            Child = _label;
        }
    }

    /// <summary>Raised the moment the button is touched or clicked.</summary>
    public event EventHandler<ButtonConfig>? Pressed;

    /// <summary>Raised when the touch or click ends, wherever it ends.</summary>
    public event EventHandler<ButtonConfig>? Released;

    /// <summary>The button as configured.</summary>
    public ButtonConfig Config { get; }

    private LabelPosition LabelPosition => Config.LabelPosition ?? _style.LabelPosition;

    /// <inheritdoc />
    protected override void OnTouchDown(TouchEventArgs e)
    {
        base.OnTouchDown(e);
        CaptureTouch(e.TouchDevice);
        BeginPress();

        // Handled, so Windows does not promote this touch into a mouse click and fire twice.
        e.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnTouchUp(TouchEventArgs e)
    {
        base.OnTouchUp(e);
        ReleaseTouchCapture(e.TouchDevice);
        EndPress();
        e.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnPreviewMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnPreviewMouseLeftButtonDown(e);
        CaptureMouse();
        BeginPress();
        e.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnPreviewMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnPreviewMouseLeftButtonUp(e);
        ReleaseMouseCapture();
        EndPress();
        e.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnLostTouchCapture(TouchEventArgs e)
    {
        base.OnLostTouchCapture(e);
        EndPress();
    }

    /// <inheritdoc />
    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        base.OnLostMouseCapture(e);
        EndPress();
    }

    private void BeginPress()
    {
        if (_isPressed)
        {
            return;
        }

        _isPressed = true;

        Background = StyleTranslator.Brush(_style.BackgroundPressed);
        _label.Foreground = StyleTranslator.Brush(_style.TextColourPressed);
        AnimateScale(_press.Scale);

        Pressed?.Invoke(this, Config);
    }

    private void EndPress()
    {
        if (!_isPressed)
        {
            return;
        }

        _isPressed = false;

        Background = StyleTranslator.Brush(_style.Background);
        _label.Foreground = StyleTranslator.Brush(_style.TextColour);
        AnimateScale(1.0);

        Released?.Invoke(this, Config);
    }

    private void AnimateScale(double target)
    {
        if (Math.Abs(_press.Scale - 1.0) < double.Epsilon)
        {
            return;
        }

        var animation = new DoubleAnimation
        {
            To = target,
            Duration = TimeSpan.FromMilliseconds(Math.Max(0, _press.DurationMs)),
            EasingFunction = StyleTranslator.Easing(_press.Easing),
            FillBehavior = FillBehavior.HoldEnd,
        };

        _scale.BeginAnimation(ScaleTransform.ScaleXProperty, animation);
        _scale.BeginAnimation(ScaleTransform.ScaleYProperty, animation);
    }
}
