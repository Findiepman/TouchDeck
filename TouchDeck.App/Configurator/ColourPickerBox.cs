using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using TouchDeck.App.Rendering;

namespace TouchDeck.App.Configurator;

/// <summary>
/// A colour, chosen from a palette or typed as hex. Leaving it empty means the theme
/// decides, so there is a way to clear it as well as a way to set it.
/// </summary>
public sealed class ColourPickerBox : Border
{
    /// <summary>The colour, as it is written in config.</summary>
    public static readonly DependencyProperty ColourProperty = DependencyProperty.Register(
        nameof(Colour),
        typeof(string),
        typeof(ColourPickerBox),
        new FrameworkPropertyMetadata(
            null,
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            OnColourChanged));

    /// <summary>
    /// Colours worth one click: a row of greys for chassis work, then hues that let a button
    /// read as danger, go, information or attention without anyone mixing a colour.
    /// </summary>
    private static readonly string[][] Palette =
    {
        new[] { "#0B0D10", "#171A1F", "#22262D", "#2E3440", "#4A5160", "#8A93A5", "#CFD4DC", "#FFFFFF" },
        new[] { "#5A1D1D", "#7A2E2E", "#B23A3A", "#E4483C", "#5A3A1D", "#8A5A1D", "#D9A441", "#F0C75E" },
        new[] { "#1D5A2C", "#2E7D4F", "#46B96B", "#6FD98F", "#1D3A5A", "#2E5F9E", "#4C9AFF", "#7FBCFF" },
        new[] { "#3A1D5A", "#5E2E8A", "#8B5CE8", "#B08CFF", "#5A1D3F", "#9E2E6A", "#FF3D7F", "#FF7FAE" },
    };

    private readonly TextBox _hex = new();
    private readonly Border _swatch = new();
    private readonly Popup _popup = new();

    private bool _updating;

    /// <summary>Creates the picker.</summary>
    public ColourPickerBox()
    {
        Background = Brushes.Transparent;
        Focusable = false;

        _hex.TextChanged += (_, _) =>
        {
            if (!_updating)
            {
                Colour = string.IsNullOrWhiteSpace(_hex.Text) ? null : _hex.Text.Trim();
            }
        };

        _swatch.Width = 34;
        _swatch.Height = 34;
        _swatch.CornerRadius = new CornerRadius(4);
        _swatch.BorderThickness = new Thickness(1);
        _swatch.Cursor = Cursors.Hand;
        _swatch.Margin = new Thickness(8, 0, 0, 0);
        _swatch.ToolTip = "Pick a colour";
        _swatch.MouseLeftButtonDown += (_, e) =>
        {
            if (_popup.Child is null)
            {
                _popup.Child = BuildPalette();
            }

            _popup.IsOpen = !_popup.IsOpen;
            e.Handled = true;
        };

        _popup.PlacementTarget = _swatch;
        _popup.Placement = PlacementMode.Bottom;
        _popup.StaysOpen = false;
        _popup.AllowsTransparency = true;
        _popup.HorizontalOffset = -244;
        _popup.VerticalOffset = 4;

        var row = new Grid();
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.Children.Add(_hex);
        Grid.SetColumn(_swatch, 1);
        row.Children.Add(_swatch);
        row.Children.Add(_popup);

        Child = row;
        Loaded += (_, _) => Paint();
    }

    /// <summary>The colour, as it is written in config. Empty means the theme decides.</summary>
    public string? Colour
    {
        get => (string?)GetValue(ColourProperty);
        set => SetValue(ColourProperty, value);
    }

    private static void OnColourChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((ColourPickerBox)d).Paint();

    private UIElement BuildPalette()
    {
        var rows = new StackPanel { Margin = new Thickness(12) };

        foreach (var line in Palette)
        {
            var strip = new StackPanel { Orientation = Orientation.Horizontal };

            foreach (var colour in line)
            {
                var value = colour;

                var chip = new Border
                {
                    Width = 26,
                    Height = 26,
                    Margin = new Thickness(0, 0, 4, 4),
                    CornerRadius = new CornerRadius(3),
                    Background = StyleTranslator.Brush(value),
                    BorderThickness = new Thickness(1),
                    BorderBrush = Resource("EdgeSoft", Color.FromRgb(0x2F, 0x2C, 0x2A)),
                    Cursor = Cursors.Hand,
                    ToolTip = value,
                };

                chip.MouseLeftButtonDown += (_, e) =>
                {
                    Colour = value;
                    _popup.IsOpen = false;
                    e.Handled = true;
                };

                strip.Children.Add(chip);
            }

            rows.Children.Add(strip);
        }

        var clear = new Button
        {
            Content = "Use the theme's colour",
            Margin = new Thickness(0, 6, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };

        clear.Click += (_, _) =>
        {
            Colour = null;
            _popup.IsOpen = false;
        };

        rows.Children.Add(clear);

        return new Border
        {
            Background = Resource("Raised", Color.FromRgb(0x2C, 0x2A, 0x28)),
            BorderBrush = Resource("Edge", Color.FromRgb(0x3A, 0x37, 0x35)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Child = rows,
        };
    }

    private void Paint()
    {
        _updating = true;

        if (_hex.Text != (Colour ?? string.Empty))
        {
            _hex.Text = Colour ?? string.Empty;
        }

        _updating = false;

        _swatch.BorderBrush = Resource("Edge", Color.FromRgb(0x3A, 0x37, 0x35));
        _swatch.Background = string.IsNullOrWhiteSpace(Colour)
            ? Resource("Field", Color.FromRgb(0x1A, 0x19, 0x17))
            : StyleTranslator.Brush(Colour);
    }

    private Brush Resource(string key, Color fallback) =>
        TryFindResource(key) as Brush ?? new SolidColorBrush(fallback);
}
