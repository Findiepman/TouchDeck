using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using TouchDeck.App.Rendering;
using TouchDeck.Core.Configuration;

namespace TouchDeck.App.Configurator;

/// <summary>Says which cell was asked for.</summary>
/// <param name="Column">Zero based column.</param>
/// <param name="Row">Zero based row.</param>
public sealed record CellEventArgs(int Column, int Row);

/// <summary>Says which two buttons should trade places.</summary>
/// <param name="Moved">The button that was dragged.</param>
/// <param name="Other">The button it was dropped onto.</param>
public sealed record SwapEventArgs(ButtonEditModel Moved, ButtonEditModel Other);

/// <summary>
/// The deck, drawn with the same layout and theme code the panel itself uses, and made
/// editable: press an empty square to put a button there, drag one to move it, drop it on
/// another to trade places.
/// </summary>
public sealed class GridEditor : Border
{
    /// <summary>How far the pointer must travel before a press becomes a drag.</summary>
    private const double DragThreshold = 6;

    private readonly DeckGrid _grid = new();

    private ProfileEditModel? _profile;
    private PageEditModel? _page;
    private ResolvedTheme _theme = Theme.Defaults.Resolve();
    private ButtonEditModel? _selected;

    private ButtonEditModel? _dragging;
    private Point _dragOrigin;
    private bool _dragStarted;

    /// <summary>Creates the grid editor.</summary>
    public GridEditor()
    {
        Child = _grid;
        ClipToBounds = true;
        Focusable = false;
        Background = Brushes.Transparent;
    }

    /// <summary>Raised when a button is pressed.</summary>
    public event EventHandler<ButtonEditModel>? ButtonSelected;

    /// <summary>Raised when an empty square is pressed.</summary>
    public event EventHandler<CellEventArgs>? EmptyCellClicked;

    /// <summary>Raised after a button is dragged onto a free square.</summary>
    public event EventHandler<ButtonEditModel>? ButtonMoved;

    /// <summary>Raised after a button is dropped onto another one.</summary>
    public event EventHandler<SwapEventArgs>? ButtonsSwapped;

    /// <summary>Raised when a button is asked to be removed from its own menu.</summary>
    public event EventHandler<ButtonEditModel>? ButtonDeleted;

    /// <summary>Raised when a button is asked to be copied from its own menu.</summary>
    public event EventHandler<ButtonEditModel>? ButtonDuplicated;

    /// <summary>The button currently highlighted, or null.</summary>
    public ButtonEditModel? Selected
    {
        get => _selected;
        set
        {
            if (!ReferenceEquals(_selected, value))
            {
                _selected = value;
                Rebuild();
            }
        }
    }

    /// <summary>Points the editor at a page and the theme it is drawn with.</summary>
    /// <param name="profile">The profile the page belongs to, for the grid size.</param>
    /// <param name="page">The page to draw, or null to clear.</param>
    /// <param name="theme">The resolved theme.</param>
    /// <param name="selected">The button to highlight, or null.</param>
    public void Show(ProfileEditModel? profile, PageEditModel? page, ResolvedTheme theme, ButtonEditModel? selected)
    {
        _profile = profile;
        _page = page;
        _theme = theme;
        _selected = selected;
        Rebuild();
    }

    /// <summary>Redraws every square from the current models.</summary>
    public void Rebuild()
    {
        _grid.Children.Clear();

        if (_profile is null || _page is null)
        {
            return;
        }

        Background = StyleTranslator.Brush(_theme.Background);

        _grid.Columns = _profile.Columns;
        _grid.Rows = _profile.Rows;
        _grid.Gap = _theme.Gap;
        _grid.EdgePadding = _theme.Padding;
        _grid.CellAspect = _profile.CellAspect;
        _grid.Fill = _profile.Fill;

        var taken = new HashSet<(int, int)>();
        foreach (var button in _page.Buttons)
        {
            for (var c = button.Col; c < button.Col + button.ColSpan; c++)
            {
                for (var r = button.Row; r < button.Row + button.RowSpan; r++)
                {
                    taken.Add((c, r));
                }
            }
        }

        for (var column = 0; column < _profile.Columns; column++)
        {
            for (var row = 0; row < _profile.Rows; row++)
            {
                if (!taken.Contains((column, row)))
                {
                    _grid.Children.Add(EmptySquare(column, row));
                }
            }
        }

        foreach (var button in _page.Buttons)
        {
            _grid.Children.Add(Tile(button));
        }
    }

    /// <summary>An empty square, which is the invitation to add a button.</summary>
    private UIElement EmptySquare(int column, int row)
    {
        var plus = new Path
        {
            Data = Geometry.Parse("M 0 9 H 18 M 9 0 V 18"),
            Stroke = Resource("InkFaint", Colors.DimGray),
            StrokeThickness = 2,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            IsHitTestVisible = false,
            Opacity = 0.55,
        };

        var square = new Border
        {
            Background = Brushes.Transparent,
            BorderBrush = Resource("EdgeSoft", Color.FromRgb(0x2F, 0x2C, 0x2A)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(_theme.Button.CornerRadius),
            Cursor = Cursors.Hand,
            Child = plus,
            ToolTip = "Add a button here",
        };

        var signal = Resource("Signal", Colors.DeepPink);

        square.MouseEnter += (_, _) =>
        {
            square.BorderBrush = signal;
            square.Background = Resource("SignalSoft", Color.FromArgb(0x3D, 0xFF, 0x3D, 0x7F));
            plus.Stroke = signal;
            plus.Opacity = 1;
        };

        square.MouseLeave += (_, _) =>
        {
            square.BorderBrush = Resource("EdgeSoft", Color.FromRgb(0x2F, 0x2C, 0x2A));
            square.Background = Brushes.Transparent;
            plus.Stroke = Resource("InkFaint", Colors.DimGray);
            plus.Opacity = 0.55;
        };

        square.PreviewMouseLeftButtonUp += (_, e) =>
        {
            e.Handled = true;
            EmptyCellClicked?.Invoke(this, new CellEventArgs(column, row));
        };

        DeckGrid.SetColumn(square, column);
        DeckGrid.SetRow(square, row);
        return square;
    }

    /// <summary>A button, drawn as the deck would draw it, with a ring when selected.</summary>
    private UIElement Tile(ButtonEditModel button)
    {
        var style = _theme.Button.With(button.Style.ToConfig());
        var isSelected = ReferenceEquals(button, _selected);
        var signal = Resource("Signal", Colors.DeepPink);

        var label = new TextBlock
        {
            Text = button.Label ?? string.Empty,
            Foreground = StyleTranslator.Brush(style.TextColour),
            FontFamily = StyleTranslator.Font(style.FontFamily),
            FontSize = style.FontSize,
            FontWeight = StyleTranslator.Weight(style.FontWeight),
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = StyleTranslator.LabelAlignment(button.LabelPosition ?? style.LabelPosition),
            IsHitTestVisible = false,
        };

        var face = new Border
        {
            Background = StyleTranslator.Brush(style.Background),
            BorderBrush = StyleTranslator.Brush(style.Border),
            BorderThickness = new Thickness(style.BorderWidth),
            CornerRadius = new CornerRadius(style.CornerRadius),
            Padding = new Thickness(style.Padding),
            Child = label,
        };

        // The ring sits outside the face, so selecting a button never changes how it looks.
        var tile = new Border
        {
            Background = Brushes.Transparent,
            BorderBrush = isSelected ? signal : Brushes.Transparent,
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(style.CornerRadius + 3),
            Padding = new Thickness(3),
            Cursor = Cursors.SizeAll,
            Child = face,
            ToolTip = Describe(button),
            ContextMenu = TileMenu(button),
        };

        if (!isSelected)
        {
            tile.MouseEnter += (_, _) => tile.BorderBrush = Resource("Edge", Color.FromRgb(0x3A, 0x37, 0x35));
            tile.MouseLeave += (_, _) => tile.BorderBrush = Brushes.Transparent;
        }

        tile.PreviewMouseLeftButtonDown += (_, e) => BeginDrag(button, tile, e);
        tile.PreviewMouseMove += (_, e) => ContinueDrag(tile, e);
        tile.PreviewMouseLeftButtonUp += (_, e) => EndDrag(tile, e);

        DeckGrid.SetColumn(tile, button.Col);
        DeckGrid.SetRow(tile, button.Row);
        DeckGrid.SetColumnSpan(tile, button.ColSpan);
        DeckGrid.SetRowSpan(tile, button.RowSpan);
        return tile;
    }

    private ContextMenu TileMenu(ButtonEditModel button)
    {
        var menu = new ContextMenu();

        var duplicate = new MenuItem { Header = "Make a copy" };
        duplicate.Click += (_, _) => ButtonDuplicated?.Invoke(this, button);

        var remove = new MenuItem { Header = "Delete this button" };
        remove.Click += (_, _) => ButtonDeleted?.Invoke(this, button);

        menu.Items.Add(duplicate);
        menu.Items.Add(remove);
        return menu;
    }

    private static string Describe(ButtonEditModel button) =>
        string.IsNullOrWhiteSpace(button.Label)
            ? button.Action.Title
            : $"{button.Label}: {button.Action.Title}";

    private void BeginDrag(ButtonEditModel button, Border tile, MouseButtonEventArgs e)
    {
        _dragging = button;
        _dragOrigin = e.GetPosition(this);
        _dragStarted = false;
        tile.CaptureMouse();
        e.Handled = true;
    }

    private void ContinueDrag(Border tile, MouseEventArgs e)
    {
        if (_dragging is null || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var position = e.GetPosition(this);

        if (_dragStarted)
        {
            return;
        }

        if (Math.Abs(position.X - _dragOrigin.X) > DragThreshold
            || Math.Abs(position.Y - _dragOrigin.Y) > DragThreshold)
        {
            _dragStarted = true;
            tile.Opacity = 0.55;
        }
    }

    private void EndDrag(Border tile, MouseButtonEventArgs e)
    {
        tile.ReleaseMouseCapture();
        tile.Opacity = 1;

        var button = _dragging;
        var started = _dragStarted;

        _dragging = null;
        _dragStarted = false;

        if (button is null)
        {
            return;
        }

        e.Handled = true;

        if (!started)
        {
            Selected = button;
            ButtonSelected?.Invoke(this, button);
            return;
        }

        if (!_grid.TryGetCell(e.GetPosition(_grid), out var column, out var row)
            || (column == button.Col && row == button.Row))
        {
            return;
        }

        if (ButtonCovering(column, row, button) is { } other)
        {
            ButtonsSwapped?.Invoke(this, new SwapEventArgs(button, other));
            return;
        }

        if (!Fits(button, column, row))
        {
            return;
        }

        button.Col = column;
        button.Row = row;
        Selected = button;
        ButtonMoved?.Invoke(this, button);
    }

    /// <summary>The button occupying a square, ignoring the one being dragged.</summary>
    private ButtonEditModel? ButtonCovering(int column, int row, ButtonEditModel ignore) =>
        _page?.Buttons.FirstOrDefault(b =>
            !ReferenceEquals(b, ignore)
            && column >= b.Col && column < b.Col + b.ColSpan
            && row >= b.Row && row < b.Row + b.RowSpan);

    /// <summary>True when a button would sit inside the grid and clear of every other button.</summary>
    private bool Fits(ButtonEditModel button, int column, int row)
    {
        if (_profile is null || _page is null)
        {
            return false;
        }

        if (column + button.ColSpan > _profile.Columns || row + button.RowSpan > _profile.Rows)
        {
            return false;
        }

        foreach (var other in _page.Buttons)
        {
            if (ReferenceEquals(other, button))
            {
                continue;
            }

            var overlapsColumns = column < other.Col + other.ColSpan && other.Col < column + button.ColSpan;
            var overlapsRows = row < other.Row + other.RowSpan && other.Row < row + button.RowSpan;

            if (overlapsColumns && overlapsRows)
            {
                return false;
            }
        }

        return true;
    }

    private Brush Resource(string key, Color fallback) =>
        TryFindResource(key) as Brush ?? new SolidColorBrush(fallback);
}
