using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using TouchDeck.App.Rendering;
using TouchDeck.Core.Configuration;

namespace TouchDeck.App.Configurator;

/// <summary>Shows a colour string as an actual swatch next to the box you type it in.</summary>
public sealed class ColourSwatchConverter : IValueConverter
{
    /// <inheritdoc />
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is string { Length: > 0 } colour ? StyleTranslator.Brush(colour) : Brushes.Transparent;

    /// <inheritdoc />
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Colours a validation message by how serious it is.</summary>
public sealed class SeverityBrushConverter : IValueConverter
{
    /// <inheritdoc />
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is ValidationSeverity.Error ? Brushes.OrangeRed : Brushes.Goldenrod;

    /// <inheritdoc />
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Shows an element only when a flag is set.</summary>
public sealed class BoolVisibilityConverter : IValueConverter
{
    /// <inheritdoc />
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var flag = value is true;
        if (parameter is string text && text.Equals("invert", StringComparison.OrdinalIgnoreCase))
        {
            flag = !flag;
        }

        return flag ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <inheritdoc />
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Renders a null enum choice as the word "inherit" rather than an empty row.</summary>
public sealed class InheritConverter : IValueConverter
{
    /// <inheritdoc />
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value?.ToString() is { Length: > 0 } text ? text : "(inherit)";

    /// <inheritdoc />
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
