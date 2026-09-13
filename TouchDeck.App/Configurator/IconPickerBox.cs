using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using TouchDeck.App.Rendering;
using TouchDeck.Core.Configuration;

namespace TouchDeck.App.Configurator;

/// <summary>
/// An icon, chosen rather than typed: a file dialog for images and a grid of glyphs for the
/// icon font, with a live preview of whatever is currently set. The value can still be typed
/// by hand, because a path with a <c>{{...}}</c> placeholder in it has no picker.
/// </summary>
public sealed class IconPickerBox : Border
{
    /// <summary>The icon value, as it is written in config.</summary>
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value),
        typeof(string),
        typeof(IconPickerBox),
        new FrameworkPropertyMetadata(
            null,
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            OnAppearanceChanged));

    /// <summary>What the value means, which decides what the picker offers.</summary>
    public static readonly DependencyProperty KindProperty = DependencyProperty.Register(
        nameof(Kind),
        typeof(IconKind),
        typeof(IconPickerBox),
        new FrameworkPropertyMetadata(IconKind.None, OnAppearanceChanged));

    /// <summary>Where relative paths are read from, and where chosen files are copied to.</summary>
    public static readonly DependencyProperty IconsDirectoryProperty = DependencyProperty.Register(
        nameof(IconsDirectory),
        typeof(string),
        typeof(IconPickerBox),
        new FrameworkPropertyMetadata(null, OnAppearanceChanged));

    /// <summary>
    /// Glyphs worth one click. These are code points rather than names: the grid draws the
    /// real glyph, so what you see is what you get, and nothing here can be mislabelled.
    /// Any other Segoe Fluent Icons code point can still be typed into the box.
    /// </summary>
    private static readonly string[] Glyphs =
    {
        "E700", "E70D", "E70E", "E70F", "E710", "E711", "E712", "E713",
        "E714", "E715", "E716", "E717", "E71A", "E71B", "E721", "E722",
        "E72C", "E72E", "E734", "E735", "E74D", "E74E", "E74F", "E767",
        "E768", "E769", "E76B", "E76C", "E77B", "E77F", "E783", "E785",
        "E786", "E7E8", "E7F4", "E7F8", "E80F", "E895", "E896", "E898",
        "E8A5", "E8B7", "E8BD", "E8C8", "E8EF", "E8FB", "E90F", "E91B",
        "E930", "E945", "E946", "E9D9", "EA80", "EB51", "EC4E", "F108",
    };

    private readonly TextBox _text = new();
    private readonly Border _preview = new();
    private readonly Button _pick = new();
    private readonly Popup _popup = new();

    private IconFactory? _factory;
    private string? _factoryRoot;
    private bool _updating;

    /// <summary>Creates the picker.</summary>
    public IconPickerBox()
    {
        Background = Brushes.Transparent;
        Focusable = false;

        _text.TextChanged += (_, _) =>
        {
            if (!_updating)
            {
                Value = string.IsNullOrWhiteSpace(_text.Text) ? null : _text.Text.Trim();
            }
        };

        _preview.Width = 34;
        _preview.Height = 34;
        _preview.CornerRadius = new CornerRadius(4);
        _preview.BorderThickness = new Thickness(1);
        _preview.Margin = new Thickness(8, 0, 0, 0);

        _pick.Content = "Choose";
        _pick.Margin = new Thickness(8, 0, 0, 0);
        _pick.Click += (_, _) => Choose();

        _popup.PlacementTarget = _preview;
        _popup.Placement = PlacementMode.Bottom;
        _popup.StaysOpen = false;
        _popup.AllowsTransparency = true;
        _popup.HorizontalOffset = -260;
        _popup.VerticalOffset = 4;

        var row = new Grid();
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        row.Children.Add(_text);
        Grid.SetColumn(_preview, 1);
        row.Children.Add(_preview);
        Grid.SetColumn(_pick, 2);
        row.Children.Add(_pick);
        row.Children.Add(_popup);

        Child = row;
        Loaded += (_, _) => Paint();
    }

    /// <summary>The icon value, as it is written in config.</summary>
    public string? Value
    {
        get => (string?)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    /// <summary>What the value means.</summary>
    public IconKind Kind
    {
        get => (IconKind)GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    /// <summary>Where relative paths are read from.</summary>
    public string? IconsDirectory
    {
        get => (string?)GetValue(IconsDirectoryProperty);
        set => SetValue(IconsDirectoryProperty, value);
    }

    private static void OnAppearanceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((IconPickerBox)d).Paint();

    private void Choose()
    {
        switch (Kind)
        {
            case IconKind.File:
                ChooseFile();
                break;

            case IconKind.Glyph:
                _popup.Child ??= BuildGlyphs();
                _popup.IsOpen = !_popup.IsOpen;
                break;

            default:
                _text.Focus();
                break;
        }
    }

    private void ChooseFile()
    {
        var extensions = string.Join(";", IconFile.SupportedExtensions.Select(e => "*" + e));

        var dialog = new OpenFileDialog
        {
            Title = "Choose an icon",
            Filter = $"Images ({extensions})|{extensions}|All files (*.*)|*.*",
            CheckFileExists = true,
        };

        if (Directory.Exists(IconsDirectory))
        {
            dialog.InitialDirectory = IconsDirectory;
        }

        if (dialog.ShowDialog() == true)
        {
            Value = Store(dialog.FileName);
        }
    }

    /// <summary>
    /// Brings a chosen file into the icons folder and returns the value to store. Copying
    /// rather than referencing means the config keeps working when the original moves, and
    /// it is what makes the icons folder the one place icons live. A file already inside the
    /// folder is referenced where it is.
    /// </summary>
    /// <param name="chosen">The file the user picked.</param>
    private string Store(string chosen)
    {
        var full = Path.GetFullPath(chosen);

        if (string.IsNullOrWhiteSpace(IconsDirectory))
        {
            return full;
        }

        var root = Path.GetFullPath(IconsDirectory);
        var prefix = root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

        if (full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return full[prefix.Length..];
        }

        try
        {
            Directory.CreateDirectory(root);

            var name = Path.GetFileName(full);
            var target = Path.Combine(root, name);

            if (File.Exists(target))
            {
                if (SameContent(full, target))
                {
                    return name;
                }

                var stem = Path.GetFileNameWithoutExtension(name);
                var extension = Path.GetExtension(name);

                for (var n = 2; File.Exists(target); n++)
                {
                    name = $"{stem} {n}{extension}";
                    target = Path.Combine(root, name);
                }
            }

            File.Copy(full, target);
            return name;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
        {
            // The absolute path still draws, so a failed copy is not worth an error dialog.
            return full;
        }
    }

    private static bool SameContent(string left, string right)
    {
        try
        {
            var a = new FileInfo(left);
            var b = new FileInfo(right);
            return a.Length == b.Length && File.ReadAllBytes(left).AsSpan().SequenceEqual(File.ReadAllBytes(right));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private UIElement BuildGlyphs()
    {
        var grid = new UniformGrid { Columns = 8, Margin = new Thickness(12) };

        foreach (var code in Glyphs)
        {
            var value = code;

            var chip = new Border
            {
                Width = 34,
                Height = 34,
                Margin = new Thickness(0, 0, 4, 4),
                CornerRadius = new CornerRadius(3),
                Background = Resource("Field", Color.FromRgb(0x1A, 0x19, 0x17)),
                BorderThickness = new Thickness(1),
                BorderBrush = Resource("EdgeSoft", Color.FromRgb(0x2F, 0x2C, 0x2A)),
                Cursor = Cursors.Hand,
                ToolTip = value,
                Child = new TextBlock
                {
                    Text = IconFactory.ParseGlyph(value) ?? string.Empty,
                    FontFamily = StyleTranslator.Font("Segoe Fluent Icons, Segoe MDL2 Assets"),
                    FontSize = 18,
                    Foreground = Resource("Ink", Color.FromRgb(0xE8, 0xEA, 0xED)),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                },
            };

            chip.MouseLeftButtonDown += (_, e) =>
            {
                Value = value;
                _popup.IsOpen = false;
                e.Handled = true;
            };

            grid.Children.Add(chip);
        }

        var panel = new StackPanel { Width = 320 };
        panel.Children.Add(grid);
        panel.Children.Add(new TextBlock
        {
            Text = "Any other Segoe Fluent Icons code point can be typed into the box.",
            Margin = new Thickness(12, 0, 12, 12),
            TextWrapping = TextWrapping.Wrap,
            Foreground = Resource("InkDim", Color.FromRgb(0x8A, 0x93, 0xA5)),
            FontSize = 11,
        });

        return new Border
        {
            Background = Resource("Raised", Color.FromRgb(0x2C, 0x2A, 0x28)),
            BorderBrush = Resource("Edge", Color.FromRgb(0x3A, 0x37, 0x35)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Child = panel,
        };
    }

    private void Paint()
    {
        _updating = true;

        if (_text.Text != (Value ?? string.Empty))
        {
            _text.Text = Value ?? string.Empty;
        }

        _updating = false;

        _pick.IsEnabled = Kind is IconKind.File or IconKind.Glyph;
        _pick.Content = Kind == IconKind.File ? "Browse" : "Choose";

        _preview.BorderBrush = Resource("Edge", Color.FromRgb(0x3A, 0x37, 0x35));
        _preview.Background = Resource("Field", Color.FromRgb(0x1A, 0x19, 0x17));
        _preview.Child = BuildPreview();
    }

    private UIElement? BuildPreview()
    {
        if (Kind == IconKind.None || string.IsNullOrWhiteSpace(Value))
        {
            return null;
        }

        var root = IconsDirectory ?? string.Empty;

        if (_factory is null || !string.Equals(_factoryRoot, root, StringComparison.OrdinalIgnoreCase))
        {
            _factory = new IconFactory(root, Serilog.Core.Logger.None);
            _factoryRoot = root;
        }

        // The preview draws at a fixed size whatever the button asks for, because it is
        // showing which icon this is rather than how big it will be.
        var style = ButtonStyle.Defaults.Resolve();
        var icon = new IconConfig { Type = Kind, Value = Value, Size = 22 };

        return _factory.Create(icon, style);
    }

    private Brush Resource(string key, Color fallback) =>
        TryFindResource(key) as Brush ?? new SolidColorBrush(fallback);
}
