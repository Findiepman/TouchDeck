using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using TouchDeck.App.Rendering;

// Aliased rather than imported: System.Windows.Shapes.Path collides with System.IO.Path, and
// importing the namespace here would set a trap for the next edit to this file.
using Ellipse = System.Windows.Shapes.Ellipse;
using Rectangle = System.Windows.Shapes.Rectangle;

namespace TouchDeck.App.Configurator;

/// <summary>
/// A colour, chosen from a saturation and brightness square with a hue strip under it, typed
/// as red, green and blue, typed as hex, or taken from a palette of colours worth one click.
/// Leaving it empty means the theme decides, so there is a way to clear it as well as a way
/// to set it.
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

    /// <summary>Width of the square, the hue strip and therefore of the popup's contents.</summary>
    private const double SurfaceWidth = 232;

    /// <summary>Height of the saturation and brightness square.</summary>
    private const double SquareHeight = 132;

    /// <summary>Height of the hue strip.</summary>
    private const double HueHeight = 14;

    /// <summary>
    /// How long after the popup closes a press on the swatch is ignored. Clicking the swatch
    /// while the palette is open closes it on the way down, and without this the release
    /// would open it straight back up.
    /// </summary>
    private const double ReopenGuardMs = 250;

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

    private readonly Rectangle _shade = new();
    private readonly Canvas _squareThumbs = new();
    private readonly Ellipse _squareThumb = new();
    private readonly Canvas _hueThumbs = new();
    private readonly Border _hueThumb = new();
    private readonly TextBox _red = new();
    private readonly TextBox _green = new();
    private readonly TextBox _blue = new();
    private readonly TextBox _code = new();

    private double _hue;
    private double _saturation;
    private double _brightness = 1;
    private byte _alpha = 255;

    private bool _updating;
    private bool _dragging;
    private DateTime _closed = DateTime.MinValue;

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

        // Opening on the release rather than the press is what makes this a click. Opening on
        // the press hands the popup the mouse midway through the gesture, and the release
        // then belongs to the popup instead of to whatever is under it.
        _swatch.PreviewMouseLeftButtonUp += (_, e) =>
        {
            Toggle();
            e.Handled = true;
        };

        _popup.PlacementTarget = _swatch;
        _popup.Placement = PlacementMode.Bottom;
        _popup.StaysOpen = false;
        _popup.AllowsTransparency = true;
        _popup.HorizontalOffset = -SurfaceWidth;
        _popup.VerticalOffset = 4;
        _popup.Closed += (_, _) => _closed = DateTime.UtcNow;

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

    /// <summary>
    /// Splits a colour into hue in degrees, saturation and brightness, each of the last two
    /// from zero to one. A grey has no hue to report, so it comes back as zero.
    /// </summary>
    /// <param name="colour">The colour to take apart.</param>
    public static (double Hue, double Saturation, double Brightness) ToHsv(Color colour)
    {
        var r = colour.R / 255.0;
        var g = colour.G / 255.0;
        var b = colour.B / 255.0;

        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var chroma = max - min;

        var hue = 0.0;

        if (chroma > 0)
        {
            if (max.Equals(r))
            {
                hue = (((g - b) / chroma) + 6) % 6;
            }
            else if (max.Equals(g))
            {
                hue = ((b - r) / chroma) + 2;
            }
            else
            {
                hue = ((r - g) / chroma) + 4;
            }

            hue *= 60;
        }

        return (hue, max <= 0 ? 0 : chroma / max, max);
    }

    /// <summary>Puts a colour back together from hue, saturation and brightness.</summary>
    /// <param name="hue">Hue in degrees; values outside zero to 360 wrap.</param>
    /// <param name="saturation">Saturation from zero to one.</param>
    /// <param name="brightness">Brightness from zero to one.</param>
    public static Color FromHsv(double hue, double saturation, double brightness)
    {
        hue = ((hue % 360) + 360) % 360;
        saturation = Math.Clamp(saturation, 0, 1);
        brightness = Math.Clamp(brightness, 0, 1);

        var chroma = brightness * saturation;
        var second = chroma * (1 - Math.Abs(((hue / 60) % 2) - 1));
        var floor = brightness - chroma;

        var (r, g, b) = (int)(hue / 60) switch
        {
            0 => (chroma, second, 0.0),
            1 => (second, chroma, 0.0),
            2 => (0.0, chroma, second),
            3 => (0.0, second, chroma),
            4 => (second, 0.0, chroma),
            _ => (chroma, 0.0, second),
        };

        static byte Channel(double value) => (byte)Math.Clamp(Math.Round(value * 255), 0, 255);

        return Color.FromRgb(Channel(r + floor), Channel(g + floor), Channel(b + floor));
    }

    /// <summary>
    /// Writes a colour the way config spells one. The alpha byte is only included when there
    /// is one to carry, so an ordinary colour stays the six digits people expect.
    /// </summary>
    /// <param name="colour">The colour to write.</param>
    public static string Format(Color colour) => colour.A == 255
        ? string.Create(CultureInfo.InvariantCulture, $"#{colour.R:X2}{colour.G:X2}{colour.B:X2}")
        : string.Create(CultureInfo.InvariantCulture, $"#{colour.A:X2}{colour.R:X2}{colour.G:X2}{colour.B:X2}");

    private static void OnColourChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((ColourPickerBox)d).Paint();

    /// <summary>
    /// Wires press, drag and release on a surface that is picked from by position. The drag
    /// takes the mouse so that running off the edge of the square keeps tracking, which is
    /// what every other colour picker does.
    /// </summary>
    /// <param name="surface">The element being picked from.</param>
    /// <param name="pick">Called with the position within the surface.</param>
    private void Track(FrameworkElement surface, Action<Point> pick)
    {
        surface.PreviewMouseLeftButtonDown += (_, e) =>
        {
            _dragging = true;
            surface.CaptureMouse();
            pick(e.GetPosition(surface));
            e.Handled = true;
        };

        surface.MouseMove += (_, e) =>
        {
            if (surface.IsMouseCaptured && e.LeftButton == MouseButtonState.Pressed)
            {
                pick(e.GetPosition(surface));
            }
        };

        surface.PreviewMouseLeftButtonUp += (_, e) =>
        {
            if (surface.IsMouseCaptured)
            {
                surface.ReleaseMouseCapture();
                e.Handled = true;
            }

            _dragging = false;
        };
    }

    private void Toggle()
    {
        if (_popup.IsOpen)
        {
            _popup.IsOpen = false;
            return;
        }

        if ((DateTime.UtcNow - _closed).TotalMilliseconds < ReopenGuardMs)
        {
            return;
        }

        _popup.Child ??= BuildPicker();
        _popup.IsOpen = true;
        Paint();
    }

    /// <summary>Takes a colour, from wherever it was chosen, and writes it out.</summary>
    /// <param name="colour">The chosen colour.</param>
    private void Choose(Color colour) => Colour = Format(colour);

    private UIElement BuildPicker()
    {
        var contents = new StackPanel { Width = SurfaceWidth, Margin = new Thickness(12) };

        contents.Children.Add(BuildSquare());
        contents.Children.Add(BuildHue());
        contents.Children.Add(BuildChannels());
        contents.Children.Add(BuildPalette());

        var clear = new Button
        {
            Content = "Use the theme's colour",
            Margin = new Thickness(0, 8, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };

        clear.Click += (_, _) =>
        {
            Colour = null;
            _popup.IsOpen = false;
        };

        contents.Children.Add(clear);

        return new Border
        {
            Background = Resource("Raised", Color.FromRgb(0x2C, 0x2A, 0x28)),
            BorderBrush = Resource("Edge", Color.FromRgb(0x3A, 0x37, 0x35)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Child = contents,
        };
    }

    /// <summary>
    /// The saturation and brightness square: the hue at full strength, washed out to white
    /// across and down to black, which is the arrangement everyone already knows.
    /// </summary>
    private UIElement BuildSquare()
    {
        _shade.Fill = new SolidColorBrush(Colors.Red);

        var wash = new Rectangle
        {
            Fill = new LinearGradientBrush(Colors.White, Color.FromArgb(0, 0xFF, 0xFF, 0xFF), 0),
        };

        var shadow = new Rectangle
        {
            Fill = new LinearGradientBrush(Color.FromArgb(0, 0, 0, 0), Colors.Black, 90),
        };

        _squareThumb.Width = 12;
        _squareThumb.Height = 12;
        _squareThumb.Stroke = Brushes.White;
        _squareThumb.StrokeThickness = 2;
        _squareThumbs.IsHitTestVisible = false;
        _squareThumbs.Children.Add(_squareThumb);

        var square = new Grid
        {
            Width = SurfaceWidth,
            Height = SquareHeight,
            Cursor = Cursors.Cross,
            Background = Brushes.Transparent,
        };

        square.Children.Add(_shade);
        square.Children.Add(wash);
        square.Children.Add(shadow);
        square.Children.Add(_squareThumbs);

        Track(square, point =>
        {
            _saturation = Math.Clamp(point.X / SurfaceWidth, 0, 1);
            _brightness = 1 - Math.Clamp(point.Y / SquareHeight, 0, 1);
            Choose(FromHsv(_hue, _saturation, _brightness));
        });

        return new Border
        {
            CornerRadius = new CornerRadius(4),
            ClipToBounds = true,
            Child = square,
        };
    }

    private UIElement BuildHue()
    {
        var spectrum = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 0) };

        for (var stop = 0; stop <= 6; stop++)
        {
            spectrum.GradientStops.Add(new GradientStop(FromHsv(stop * 60, 1, 1), stop / 6.0));
        }

        _hueThumb.Width = 5;
        _hueThumb.Height = HueHeight + 6;
        _hueThumb.BorderBrush = Brushes.White;
        _hueThumb.BorderThickness = new Thickness(2);
        _hueThumb.CornerRadius = new CornerRadius(3);
        Canvas.SetTop(_hueThumb, -3);

        _hueThumbs.IsHitTestVisible = false;
        _hueThumbs.Children.Add(_hueThumb);

        var strip = new Grid
        {
            Width = SurfaceWidth,
            Height = HueHeight,
            Cursor = Cursors.Cross,
            Background = Brushes.Transparent,
            Margin = new Thickness(0, 10, 0, 0),
        };

        strip.Children.Add(new Rectangle { Fill = spectrum, RadiusX = 3, RadiusY = 3 });
        strip.Children.Add(_hueThumbs);

        Track(strip, point =>
        {
            _hue = Math.Clamp(point.X / SurfaceWidth, 0, 1) * 360;
            Choose(FromHsv(_hue, _saturation, _brightness));
        });

        return strip;
    }

    /// <summary>The same colour as numbers, for when it has to match something exactly.</summary>
    private UIElement BuildChannels()
    {
        var channels = new Grid { Margin = new Thickness(0, 12, 0, 0) };

        for (var column = 0; column < 6; column++)
        {
            channels.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = column % 2 == 0 ? GridLength.Auto : new GridLength(1, GridUnitType.Star),
            });
        }

        channels.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        channels.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        Place(_red, "R", 0);
        Place(_green, "G", 2);
        Place(_blue, "B", 4);

        void Place(TextBox box, string name, int column)
        {
            var label = new TextBlock
            {
                Text = name,
                Margin = new Thickness(column == 0 ? 0 : 8, 0, 6, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = Resource("InkDim", Color.FromRgb(0x9C, 0x94, 0x8A)),
                FontSize = 11,
            };

            Grid.SetColumn(label, column);
            channels.Children.Add(label);

            box.TextAlignment = TextAlignment.Center;
            box.TextChanged += (_, _) => OnChannelTyped();
            Grid.SetColumn(box, column + 1);
            channels.Children.Add(box);
        }

        _code.Margin = new Thickness(0, 8, 0, 0);
        _code.ToolTip = "Hex, as #RRGGBB.";
        _code.TextChanged += (_, _) =>
        {
            if (!_updating && StyleTranslator.TryColour(_code.Text, out var typed))
            {
                Choose(Color.FromArgb(_alpha, typed.R, typed.G, typed.B));
            }
        };

        Grid.SetRow(_code, 1);
        Grid.SetColumnSpan(_code, 6);
        channels.Children.Add(_code);

        return channels;
    }

    private void OnChannelTyped()
    {
        if (_updating)
        {
            return;
        }

        if (Byte(_red) is not { } r || Byte(_green) is not { } g || Byte(_blue) is not { } b)
        {
            return;
        }

        Choose(Color.FromArgb(_alpha, r, g, b));

        static byte? Byte(TextBox box) =>
            byte.TryParse(box.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
                ? value
                : null;
    }

    private UIElement BuildPalette()
    {
        var rows = new StackPanel { Margin = new Thickness(0, 12, 0, 0) };

        foreach (var line in Palette)
        {
            var strip = new StackPanel { Orientation = Orientation.Horizontal };

            foreach (var colour in line)
            {
                var value = colour;

                var chip = new Border
                {
                    Width = 25,
                    Height = 25,
                    Margin = new Thickness(0, 0, 4, 4),
                    CornerRadius = new CornerRadius(3),
                    Background = StyleTranslator.Brush(value),
                    BorderThickness = new Thickness(1),
                    BorderBrush = Resource("EdgeSoft", Color.FromRgb(0x2F, 0x2C, 0x2A)),
                    Cursor = Cursors.Hand,
                    ToolTip = value,
                };

                // On the release, so that a plain click picks. See the swatch handler.
                chip.PreviewMouseLeftButtonUp += (_, e) =>
                {
                    Colour = value;
                    _popup.IsOpen = false;
                    e.Handled = true;
                };

                strip.Children.Add(chip);
            }

            rows.Children.Add(strip);
        }

        return rows;
    }

    private void Paint()
    {
        _updating = true;

        if (_hex.Text != (Colour ?? string.Empty))
        {
            _hex.Text = Colour ?? string.Empty;
        }

        var colour = default(Color);
        var known = !string.IsNullOrWhiteSpace(Colour) && StyleTranslator.TryColour(Colour, out colour);

        // While the square or the strip is being dragged the hue and brightness on screen are
        // the truth and the colour is derived from them. Reading them back would throw the
        // hue away every time the drag passed through black or white.
        if (known && !_dragging)
        {
            (_hue, _saturation, _brightness) = ToHsv(colour);
            _alpha = colour.A;
        }

        if (_popup.Child is not null)
        {
            _shade.Fill = new SolidColorBrush(FromHsv(_hue, 1, 1));

            Canvas.SetLeft(_squareThumb, (_saturation * SurfaceWidth) - (_squareThumb.Width / 2));
            Canvas.SetTop(_squareThumb, ((1 - _brightness) * SquareHeight) - (_squareThumb.Height / 2));
            Canvas.SetLeft(_hueThumb, (_hue / 360 * SurfaceWidth) - (_hueThumb.Width / 2));

            _red.Text = known ? colour.R.ToString(CultureInfo.InvariantCulture) : string.Empty;
            _green.Text = known ? colour.G.ToString(CultureInfo.InvariantCulture) : string.Empty;
            _blue.Text = known ? colour.B.ToString(CultureInfo.InvariantCulture) : string.Empty;

            var code = known ? Format(colour) : string.Empty;

            if (!_code.Text.Equals(code, StringComparison.OrdinalIgnoreCase))
            {
                _code.Text = code;
            }
        }

        _updating = false;

        _swatch.BorderBrush = Resource("Edge", Color.FromRgb(0x3A, 0x37, 0x35));
        _swatch.Background = known
            ? new SolidColorBrush(colour)
            : Resource("Field", Color.FromRgb(0x1A, 0x19, 0x17));
    }

    private Brush Resource(string key, Color fallback) =>
        TryFindResource(key) as Brush ?? new SolidColorBrush(fallback);
}
