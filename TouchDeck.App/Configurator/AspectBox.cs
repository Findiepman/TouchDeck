using System.Windows;
using System.Windows.Controls;

namespace TouchDeck.App.Configurator;

/// <summary>
/// Holds its child at a fixed shape and centres it. The preview uses it to take the shape
/// of the actual touchscreen, so the config center shows the deck the size it will be.
/// </summary>
public sealed class AspectBox : Decorator
{
    /// <summary>Width divided by height.</summary>
    public static readonly DependencyProperty AspectProperty = DependencyProperty.Register(
        nameof(Aspect),
        typeof(double),
        typeof(AspectBox),
        new FrameworkPropertyMetadata(16.0 / 9.0, FrameworkPropertyMetadataOptions.AffectsMeasure));

    /// <summary>Width divided by height. Values of zero or less are ignored.</summary>
    public double Aspect
    {
        get => (double)GetValue(AspectProperty);
        set => SetValue(AspectProperty, value);
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size constraint)
    {
        var size = Fit(constraint);
        Child?.Measure(size);
        return size;
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size arrangeSize)
    {
        var size = Fit(arrangeSize);

        Child?.Arrange(new Rect(
            new Point((arrangeSize.Width - size.Width) / 2, (arrangeSize.Height - size.Height) / 2),
            size));

        return arrangeSize;
    }

    /// <summary>The largest box of the right shape that fits in the space available.</summary>
    private Size Fit(Size available)
    {
        var aspect = Aspect > 0 ? Aspect : 16.0 / 9.0;

        var width = double.IsInfinity(available.Width) ? 0 : available.Width;
        var height = double.IsInfinity(available.Height) ? 0 : available.Height;

        if (width <= 0 || height <= 0)
        {
            return new Size(width, height);
        }

        return width / aspect <= height
            ? new Size(width, width / aspect)
            : new Size(height * aspect, height);
    }
}
