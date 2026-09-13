using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
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

    /// <summary>The icon fonts to offer glyphs from, best first.</summary>
    private static readonly string[] GlyphFonts = { "Segoe Fluent Icons", "Segoe MDL2 Assets" };

    /// <summary>
    /// The glyphs worth putting first. These are code points rather than names: the grid
    /// draws the real glyph, so what you see is what you get, and nothing here can be
    /// mislabelled. Anything in this list the font turns out not to have is dropped, so an
    /// older Windows shows a shorter row rather than a row of empty boxes.
    /// </summary>
    private static readonly string[] Common =
    {
        // Decks, apps and windows.
        "E700", "E71D", "E713", "E7EE", "E737", "E71E", "E7C4", "E8B7", "E8A9", "E8FD",
        "E7C5", "E8B0", "E770", "E80F", "E8A1", "E7F4", "E721", "E794", "E71C", "E8AB",

        // Media and sound.
        "E768", "E769", "E71A", "E893", "E892", "E100", "E101", "E102", "E103", "E104",
        "E767", "E74F", "E995", "E994", "E993", "E992", "E720", "EC54", "EC55", "E7F6",
        "E8D6", "E8B2", "E90A", "E90B", "E714", "E786", "EA69", "E8AA", "E93C", "E9D9",

        // Communication.
        "E715", "E716", "E717", "E8BD", "E8F2", "E8C9", "E8BA", "E910", "E77B", "E13D",
        "E7E7", "E8C8", "E8C6", "E8C1", "E8AC", "E74D", "E74E", "E74B", "E74A",

        // Movement and arrows.
        "E70D", "E70E", "E76B", "E76C", "E72B", "E72A", "E72C", "E895", "E896", "E898",
        "E946", "E945", "E930", "E7A7", "E7A6", "E72E", "E785", "E72E", "E1CB", "E1CE",

        // Status and signs.
        "E734", "E735", "E8D9", "E930", "E9CE", "EA39", "E783", "E7BA", "E7BC", "E946",
        "E10B", "E10A", "E73E", "E711", "E710", "E712", "E738", "E8FB", "E894", "E8BB",

        // Things and places.
        "E80F", "E707", "E718", "E719", "E722", "E7B5", "E7C3", "E7EE", "E77F", "E7B8",
        "E706", "E708", "E709", "E70A", "E701", "E702", "E703", "E704", "E705", "E753",
    };

    /// <summary>Every glyph the icon font actually has, worked out once.</summary>
    private static readonly Lazy<GlyphSet> Available = new(FindGlyphs);

    private readonly TextBox _text = new();
    private readonly Border _preview = new();
    private readonly Button _pick = new();
    private readonly Button _cut = new();
    private readonly Button _clear = new();
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

        _preview.Width = 64;
        _preview.Height = 64;
        _preview.CornerRadius = new CornerRadius(4);
        _preview.BorderThickness = new Thickness(1);
        _preview.Margin = new Thickness(10, 0, 0, 0);

        _pick.Content = "Choose";
        _pick.Click += (_, _) => Choose();

        _cut.Content = "No background";
        _cut.Margin = new Thickness(8, 0, 0, 0);
        _cut.ToolTip = "Make the flat background transparent and trim the empty margin.";
        _cut.Click += (_, _) => CutOutCurrent();

        _clear.Content = "\uE711";
        _clear.FontFamily = StyleTranslator.Font("Segoe Fluent Icons, Segoe MDL2 Assets");
        _clear.Margin = new Thickness(8, 0, 0, 0);
        _clear.ToolTip = "Take this icon off the button.";
        _clear.SetResourceReference(StyleProperty, "Button.Square");
        _clear.Click += (_, _) => Value = null;

        _popup.PlacementTarget = _preview;
        _popup.Placement = PlacementMode.Bottom;
        _popup.StaysOpen = false;
        _popup.AllowsTransparency = true;
        _popup.HorizontalOffset = -(GlyphGridWidth - 64);
        _popup.VerticalOffset = 4;

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 8, 0, 0),
        };

        buttons.Children.Add(_pick);
        buttons.Children.Add(_cut);
        buttons.Children.Add(_clear);

        var layout = new Grid();
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        layout.Children.Add(_text);
        Grid.SetRow(buttons, 1);
        layout.Children.Add(buttons);
        Grid.SetColumn(_preview, 1);
        Grid.SetRowSpan(_preview, 2);
        layout.Children.Add(_preview);
        layout.Children.Add(_popup);

        Child = layout;
        Loaded += (_, _) => Paint();
    }

    /// <summary>How wide the glyph grid is, which is ten chips and the padding round them.</summary>
    private static double GlyphGridWidth => (GlyphColumns * GlyphChip) + 24;

    /// <summary>Chips across the glyph grid.</summary>
    private const int GlyphColumns = 10;

    /// <summary>Size of one glyph chip, its margin included.</summary>
    private const double GlyphChip = 34;

    /// <summary>How many chips are built before the dispatcher gets a turn.</summary>
    private const int GlyphBatch = 200;

    /// <summary>The glyphs the grid puts first, with anything the icon font lacks dropped.</summary>
    public static IReadOnlyList<string> CommonGlyphs => Available.Value.Common;

    /// <summary>Every glyph the icon font offers, which is the rest of the grid.</summary>
    public static IReadOnlyList<string> FontGlyphs => Available.Value.All;

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

    /// <summary>
    /// Every glyph in the icon font, rather than a list written out by hand. Reading the
    /// font's own character map means the grid cannot claim a glyph the font does not have,
    /// and it grows by itself when Windows ships more of them.
    /// </summary>
    private static GlyphSet FindGlyphs()
    {
        foreach (var name in GlyphFonts)
        {
            var family = Fonts.SystemFontFamilies.FirstOrDefault(f => f.FamilyNames.Values.Any(
                n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase)));

            if (family is null)
            {
                continue;
            }

            foreach (var typeface in family.GetTypefaces())
            {
                if (!typeface.TryGetGlyphTypeface(out var glyphs))
                {
                    continue;
                }

                // The private use area is where an icon font keeps its symbols. Everything
                // outside it is the handful of real characters the font also carries.
                var codes = glyphs.CharacterToGlyphMap.Keys
                    .Where(c => c is >= 0xE000 and <= 0xF8FF)
                    .OrderBy(c => c)
                    .Select(c => c.ToString("X4", CultureInfo.InvariantCulture))
                    .ToArray();

                if (codes.Length > 0)
                {
                    var held = new HashSet<string>(codes, StringComparer.OrdinalIgnoreCase);

                    return new GlyphSet(
                        name,
                        Common.Where(held.Contains).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
                        codes);
                }
            }
        }

        // No icon font to read, so the hand written list is all there is. It will draw as
        // empty boxes, which is already what a button using one of them looks like.
        return new GlyphSet(null, Common.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(), Array.Empty<string>());
    }

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
    /// it is what makes the icons folder the one place icons live.
    ///
    /// A flat background is taken off on the way in, and the empty margin round what is left
    /// is trimmed. Deck buttons are dark and most logos are downloaded on white, so without
    /// this the usual result is a white card with a small picture in the middle of it. The
    /// prepared image is written as a new file; nothing already in the folder, and certainly
    /// not the file that was picked, is ever written over.
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
        var inside = full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);

        try
        {
            Directory.CreateDirectory(root);

            // Preparing happens whether or not the file had to be copied. The file dialog
            // opens in the icons folder, so picking something already there is the most
            // likely thing to do, and it used to be the one path that skipped this.
            if (CutOut(full) is { } cut)
            {
                var prepared = Free(root, Path.GetFileNameWithoutExtension(full) + ".png", full);
                IconImage.SavePng(cut, Path.Combine(root, prepared));
                return prepared;
            }

            if (inside)
            {
                return full[prefix.Length..];
            }

            var name = Path.GetFileName(full);

            if (File.Exists(Path.Combine(root, name)) && SameContent(full, Path.Combine(root, name)))
            {
                return name;
            }

            name = Free(root, name, full);
            File.Copy(full, Path.Combine(root, name));
            return name;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
        {
            // The absolute path still draws, so a failed copy is not worth an error dialog.
            return inside ? full[prefix.Length..] : full;
        }
    }

    /// <summary>
    /// Takes the background off whatever the icon currently points at, and points it at the
    /// result. This exists because preparing on the way in only helps an icon that arrived
    /// that way: one typed in by hand, or set before this could do it, would otherwise have
    /// no way to get the same treatment short of picking the file again.
    /// </summary>
    private void CutOutCurrent()
    {
        if (Kind != IconKind.File ||
            string.IsNullOrWhiteSpace(Value) ||
            string.IsNullOrWhiteSpace(IconsDirectory) ||
            IconFile.Resolve(IconsDirectory, Value) is not { } path)
        {
            return;
        }

        if (!File.Exists(path))
        {
            Explain($"There is no file at {path}.");
            return;
        }

        if (CutOut(path) is not { } cut)
        {
            Explain(
                "This image has no flat background to remove and no empty margin to trim. " +
                "A background is only removed when the edges of the image are all one colour.");
            return;
        }

        try
        {
            var root = Path.GetFullPath(IconsDirectory);
            Directory.CreateDirectory(root);

            var name = Free(root, Path.GetFileNameWithoutExtension(path) + ".png", path);
            IconImage.SavePng(cut, Path.Combine(root, name));
            Value = name;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
        {
            Explain($"The prepared image could not be written: {e.Message}");
        }
    }

    private static void Explain(string message) =>
        MessageBox.Show(message, "Remove background", MessageBoxButton.OK, MessageBoxImage.Information);

    /// <summary>
    /// A file name in <paramref name="root"/> that is not taken, and is never the file being
    /// read from, so preparing an image cannot overwrite the image it came from.
    /// </summary>
    /// <param name="root">The icons folder.</param>
    /// <param name="name">The name to use if it is free.</param>
    /// <param name="source">The file being read, which must not be the answer.</param>
    private static string Free(string root, string name, string source)
    {
        var stem = Path.GetFileNameWithoutExtension(name);
        var extension = Path.GetExtension(name);
        var candidate = name;

        bool Taken(string full) =>
            File.Exists(full) ||
            string.Equals(Path.GetFullPath(full), source, StringComparison.OrdinalIgnoreCase);

        for (var n = 2; Taken(Path.Combine(root, candidate)); n++)
        {
            candidate = $"{stem} {n}{extension}";
        }

        return candidate;
    }

    /// <summary>
    /// The image prepared for use as an icon, or null when there was nothing to do and it
    /// should be left exactly as it is.
    /// </summary>
    /// <param name="path">An absolute path to an image file.</param>
    private static BitmapSource? CutOut(string path)
    {
        if (!IconFile.IsSupported(path))
        {
            return null;
        }

        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.UriSource = new Uri(path, UriKind.Absolute);
            image.EndInit();

            return IconImage.Prepare(image);
        }
        catch (Exception e) when (e is NotSupportedException or IOException or UnauthorizedAccessException
                                      or ArgumentException or UriFormatException)
        {
            return null;
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
        var glyphs = Available.Value;
        var contents = new StackPanel { Width = GlyphGridWidth };

        contents.Children.Add(Heading("The ones you probably want", first: true));
        contents.Children.Add(Chips(glyphs.Common));

        if (glyphs.All.Count > 0)
        {
            contents.Children.Add(Heading($"Everything in {glyphs.Font} — {glyphs.All.Count}", first: false));
            contents.Children.Add(Chips(glyphs.All));
        }

        contents.Children.Add(new TextBlock
        {
            Text = "Any code point can also be typed into the box, as E713 or U+E713.",
            Margin = new Thickness(12, 8, 12, 12),
            TextWrapping = TextWrapping.Wrap,
            Foreground = Resource("InkDim", Color.FromRgb(0x9C, 0x94, 0x8A)),
            FontSize = 11,
        });

        return new Border
        {
            Background = Resource("Raised", Color.FromRgb(0x2C, 0x2A, 0x28)),
            BorderBrush = Resource("Edge", Color.FromRgb(0x3A, 0x37, 0x35)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Child = new ScrollViewer
            {
                MaxHeight = 460,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Content = contents,
            },
        };
    }

    private TextBlock Heading(string text, bool first) => new()
    {
        Text = text,
        Margin = new Thickness(12, first ? 12 : 14, 12, 8),
        Foreground = Resource("InkDim", Color.FromRgb(0x9C, 0x94, 0x8A)),
        FontFamily = TryFindResource("Legend") as FontFamily ?? new FontFamily("Segoe UI"),
        FontSize = 11,
    };

    private UIElement Chips(IReadOnlyList<string> codes)
    {
        var grid = new UniformGrid { Columns = GlyphColumns, Margin = new Thickness(12, 0, 12, 0) };
        Fill(grid, codes, 0);
        return grid;
    }

    /// <summary>
    /// Adds chips a few rows at a time, handing the dispatcher back between batches. The font
    /// carries a couple of thousand glyphs and building that many controls in one go is long
    /// enough to feel: this way the grid is on screen immediately and finishes filling itself
    /// in underneath the scroll bar.
    /// </summary>
    /// <param name="grid">The grid being filled.</param>
    /// <param name="codes">Every code point to add.</param>
    /// <param name="from">Where this batch starts.</param>
    private void Fill(Panel grid, IReadOnlyList<string> codes, int from)
    {
        var to = Math.Min(codes.Count, from + GlyphBatch);

        for (var index = from; index < to; index++)
        {
            grid.Children.Add(Chip(codes[index]));
        }

        if (to < codes.Count)
        {
            Dispatcher.InvokeAsync(() => Fill(grid, codes, to), DispatcherPriority.Background);
        }
    }

    private Border Chip(string code)
    {
        var chip = new Border
        {
            Width = GlyphChip - 4,
            Height = GlyphChip - 4,
            Margin = new Thickness(0, 0, 4, 4),
            CornerRadius = new CornerRadius(3),
            Background = Resource("Field", Color.FromRgb(0x1A, 0x19, 0x17)),
            BorderThickness = new Thickness(1),
            BorderBrush = Resource("EdgeSoft", Color.FromRgb(0x2F, 0x2C, 0x2A)),
            Cursor = Cursors.Hand,
            ToolTip = code,
            Child = new TextBlock
            {
                Text = IconFactory.ParseGlyph(code) ?? string.Empty,
                FontFamily = StyleTranslator.Font("Segoe Fluent Icons, Segoe MDL2 Assets"),
                FontSize = 17,
                Foreground = Resource("Ink", Color.FromRgb(0xEF, 0xE9, 0xE0)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            },
        };

        // On the release, so that a plain click picks rather than a press and hold.
        chip.PreviewMouseLeftButtonUp += (_, e) =>
        {
            Value = code;
            _popup.IsOpen = false;
            e.Handled = true;
        };

        return chip;
    }

    private void Paint()
    {
        _updating = true;

        if (_text.Text != (Value ?? string.Empty))
        {
            _text.Text = Value ?? string.Empty;
        }

        _updating = false;

        var value = Value;

        _pick.IsEnabled = Kind is IconKind.File or IconKind.Glyph;
        _pick.Content = Kind == IconKind.File ? "Browse" : "Choose";

        _cut.Visibility = Kind == IconKind.File ? Visibility.Visible : Visibility.Collapsed;
        _cut.IsEnabled = !string.IsNullOrWhiteSpace(value) && !IconFile.IsInterpolated(value);

        _clear.IsEnabled = !string.IsNullOrWhiteSpace(value);

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
        var icon = new IconConfig { Type = Kind, Value = Value, Size = 48 };

        return _factory.Create(icon, style);
    }

    private Brush Resource(string key, Color fallback) =>
        TryFindResource(key) as Brush ?? new SolidColorBrush(fallback);

    /// <summary>The glyphs an icon font offers, split into the handy ones and all of them.</summary>
    /// <param name="Font">The font they came from, or null when none was found.</param>
    /// <param name="Common">The curated list, with anything the font lacks dropped.</param>
    /// <param name="All">Every private use code point in the font.</param>
    private sealed record GlyphSet(string? Font, IReadOnlyList<string> Common, IReadOnlyList<string> All);
}
