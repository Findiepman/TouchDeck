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
    /// <summary>Space between the icon and the label when both are shown.</summary>
    private const double LabelGap = 4;

    /// <summary>
    /// Arranges whichever of the icon and the label exist, or returns null when neither
    /// does. Top and bottom pin the label to that edge and give the icon the rest;
    /// <c>center</c> stacks the two and centres them together as one block.
    /// </summary>
    /// <param name="label">The label, or null when the button has none.</param>
    /// <param name="icon">The icon visual, or null when there is none.</param>
    /// <param name="position">Where the label sits.</param>
    public static UIElement? Compose(TextBlock? label, FrameworkElement? icon, LabelPosition position)
    {
        if (position == LabelPosition.None || label is not { Text.Length: > 0 })
        {
            return icon;
        }

        if (icon is null)
        {
            label.VerticalAlignment = StyleTranslator.LabelAlignment(position);
            return label;
        }

        label.VerticalAlignment = VerticalAlignment.Center;

        if (position == LabelPosition.Center)
        {
            var centred = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                IsHitTestVisible = false,
            };

            label.Margin = new Thickness(0, LabelGap, 0, 0);
            centred.Children.Add(icon);
            centred.Children.Add(label);
            return centred;
        }

        var docked = new DockPanel { LastChildFill = true, IsHitTestVisible = false };

        label.Margin = position == LabelPosition.Top
            ? new Thickness(0, 0, 0, LabelGap)
            : new Thickness(0, LabelGap, 0, 0);

        DockPanel.SetDock(label, position == LabelPosition.Top ? Dock.Top : Dock.Bottom);

        docked.Children.Add(label);
        docked.Children.Add(icon);
        return docked;
    }
}
