using System.Windows;
using System.Windows.Controls;
using TouchDeck.Core.Configuration;

namespace TouchDeck.App.Rendering;

/// <summary>
/// How an icon and a label sit inside a button. The deck and the config center's preview
/// both use this, because a preview that arranges things its own way is worse than no
/// preview at all.
/// </summary>
public static class ButtonContent
{
    /// <summary>
    /// Arranges whichever of the icon and the label exist, or returns null when neither
    /// does. Top and bottom pin the label to that edge and give the icon the rest;
    /// <c>center</c> stacks the two and centres them together as one block. In every case
    /// the icon is bounded by what is left of the button, so an icon size larger than the
    /// button means "as large as fits" rather than spilling over the edge.
    /// </summary>
    /// <param name="label">The label, or null when the button has none.</param>
    /// <param name="icon">The icon visual, or null when there is none.</param>
    /// <param name="position">Where the label sits.</param>
    public static UIElement? Compose(TextBlock? label, FrameworkElement? icon, LabelPosition position)
    {
        var visible = position != LabelPosition.None && label is { Text.Length: > 0 } ? label : null;

        if (visible is null && icon is null)
        {
            return null;
        }

        if (visible is not null)
        {
            // The panel places the label itself, so its own alignment must not fight it.
            visible.VerticalAlignment = VerticalAlignment.Top;
        }

        return new IconLabelPanel(icon, visible, position);
    }
}
