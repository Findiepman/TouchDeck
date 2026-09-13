using System.Windows;
using System.Windows.Controls;
using TouchDeck.Core.Configuration;

namespace TouchDeck.App.Rendering;

/// <summary>
/// Lays an icon and a label out inside a button. It exists because the stock panels each get
/// one half of this wrong: a <see cref="StackPanel"/> offers its children unlimited height,
/// so a large icon grows past the button and takes the label off the bottom edge, while a
/// <see cref="DockPanel"/> bounds the icon properly but can only pin the pair to an edge.
/// Here the label is measured first and the icon is given exactly what is left, so a size
/// larger than the button simply means "as large as fits".
/// </summary>
public sealed class IconLabelPanel : Panel
{
    /// <summary>Space between the icon and the label when both are shown.</summary>
    private const double Gap = 4;

    private readonly UIElement? _icon;
    private readonly UIElement? _label;
    private readonly LabelPosition _position;

    /// <summary>Creates the layout.</summary>
    /// <param name="icon">The icon visual, or null when there is none.</param>
    /// <param name="label">The label, or null when there is none.</param>
    /// <param name="position">Where the label sits.</param>
    public IconLabelPanel(UIElement? icon, UIElement? label, LabelPosition position)
    {
        _icon = icon;
        _label = label;
        _position = position;

        IsHitTestVisible = false;

        if (icon is not null)
        {
            Children.Add(icon);
        }

        if (label is not null)
        {
            Children.Add(label);
        }
    }

    /// <summary>Space taken by the gap, which only exists when both parts do.</summary>
    private double Spacing => _icon is not null && _label is not null ? Gap : 0;

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        var labelHeight = 0.0;

        if (_label is not null)
        {
            // The label wraps to the available width and takes whatever height that needs,
            // which is why it is measured before the icon rather than sharing the space.
            _label.Measure(new Size(availableSize.Width, double.PositiveInfinity));
            labelHeight = _label.DesiredSize.Height;
        }

        if (_icon is not null)
        {
            var height = double.IsInfinity(availableSize.Height)
                ? double.PositiveInfinity
                : Math.Max(0, availableSize.Height - labelHeight - Spacing);

            _icon.Measure(new Size(availableSize.Width, height));
        }

        var iconSize = _icon?.DesiredSize ?? default;

        return new Size(
            Math.Max(iconSize.Width, _label?.DesiredSize.Width ?? 0),
            iconSize.Height + labelHeight + Spacing);
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        var labelHeight = _label?.DesiredSize.Height ?? 0;

        if (_icon is null)
        {
            _label?.Arrange(new Rect(0, LabelTop(finalSize, labelHeight), finalSize.Width, labelHeight));
            return finalSize;
        }

        if (_label is null)
        {
            _icon.Arrange(new Rect(0, 0, finalSize.Width, finalSize.Height));
            return finalSize;
        }

        var available = Math.Max(0, finalSize.Height - labelHeight - Spacing);

        switch (_position)
        {
            case LabelPosition.Top:
                _label.Arrange(new Rect(0, 0, finalSize.Width, labelHeight));
                _icon.Arrange(new Rect(0, labelHeight + Spacing, finalSize.Width, available));
                break;

            case LabelPosition.Center:
                // Centred means the pair is centred as one block, so the icon gets only the
                // height it actually wants and the group is placed around that.
                var iconHeight = Math.Min(_icon.DesiredSize.Height, available);
                var top = Math.Max(0, (finalSize.Height - (iconHeight + Spacing + labelHeight)) / 2);

                _icon.Arrange(new Rect(0, top, finalSize.Width, iconHeight));
                _label.Arrange(new Rect(0, top + iconHeight + Spacing, finalSize.Width, labelHeight));
                break;

            default:
                _icon.Arrange(new Rect(0, 0, finalSize.Width, available));
                _label.Arrange(new Rect(0, finalSize.Height - labelHeight, finalSize.Width, labelHeight));
                break;
        }

        return finalSize;
    }

    /// <summary>Where a label with no icon sits.</summary>
    private double LabelTop(Size finalSize, double labelHeight) => _position switch
    {
        LabelPosition.Top => 0,
        LabelPosition.Center => Math.Max(0, (finalSize.Height - labelHeight) / 2),
        _ => Math.Max(0, finalSize.Height - labelHeight),
    };
}
