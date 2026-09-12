using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using TouchDeck.App.Rendering;
using TouchDeck.Core.Configuration;

namespace TouchDeck.App.Configurator;

/// <summary>Says which cell was asked for.</summary>
/// <param name="Column">Zero based column.</param>
/// <param name="Row">Zero based row.</param>
public sealed record CellEventArgs(int Column, int Row);

/// <summary>
/// The visual grid in the config center: the same layout the panel uses, with empty cells
/// you can click to add a button and buttons you can drag to move.
/// </summary>
public sealed class GridEditor : Border
{
    /// <summary>How far the pointer must travel before a click becomes a drag.</summary>
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

    /// <summary>Raised when a button tile is clicked.</summary>
    public event EventHandler<ButtonEditModel>? ButtonSelected;

    /// <summary>Raised when an empty cell is clicked.</summary>
    public event EventHandler<CellEventArgs>? EmptyCellClicked;

    /// <summary>Raised after a button is dragged onto a different cell.</summary>
    public event EventHandler<ButtonEditModel>? ButtonMoved;

    /// <summary>The button currently highlighted, or null.</summary>
    public ButtonEditModel? Selected
    {
        get => _selected;
        set
        {
            _selected = value;
            Rebuild();
        }
    }

    /// <summary>Points the editor at a page and the theme it is drawn with.</summary>
    /// <param name="profile">The profile the page belongs to, for the grid size.</param>
    /// <param name="page">The page to draw, or null to clear.</param>
    /// <param name="theme">The resolved theme.</param>
    public void Show(ProfileEditModel? profile, PageEditModel? page, ResolvedTheme theme)
    {
        _profile = profile;
        _page = page;
        _theme = theme;
        Rebuild();
    }

    /// <summary>Redraws every tile from the current models.</summary>
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
                if (taken.Contains((column, row)))
                {
                    continue;
                }

                _grid.Children.Add(CreateEmptyCell(column, row));
            }
        }

        foreach (var button in _page.Buttons)
        {
            _grid.Children.Add(CreateTile(button));
        }
    }

    private UIElement CreateEmptyCell(int column, int row)
    {
        var cell = new Border
        {
            Background = Brushes.Transparent,
            BorderBrush = StyleTranslator.Brush("#3322262D"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(_theme.Button.CornerRadius),
            Child = new TextBlock
            {
                Text = "+",
                FontSize = 20,
                Foreground = StyleTranslator.Brush("#5522262D"),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                IsHitTestVisible = false,
            },
            Cursor = Cursors.Hand,
            ToolTip = $"Add a button at column {column}, row {row}",
        };

        cell.MouseLeftButtonUp += (_, _) => EmptyCellClicked?.Invoke(this, new CellEventArgs(column, row));

        DeckGrid.SetColumn(cell, column);
        DeckGrid.SetRow(cell, row);
        return cell;
    }

    private UIElement CreateTile(ButtonEditModel button)
    {
        var style = _theme.Button.With(button.Style.ToConfig());
        var isSelected = ReferenceEquals(button, _selected);

        var tile = new Border
        {
            Background = StyleTranslator.Brush(style.Background),
            BorderBrush = isSelected ? StyleTranslator.Brush("#4C9AFF") : StyleTranslator.Brush(style.Border),
            BorderThickness = new Thickness(isSelected ? 3 : style.BorderWidth),
            CornerRadius = new CornerRadius(style.CornerRadius),
            Padding = new Thickness(style.Padding),
            Cursor = Cursors.SizeAll,
            Child = new TextBlock
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
            },
            ToolTip = $"{button.DisplayName}  -  {button.Action.Type}",
        };

        tile.MouseLeftButtonDown += (_, e) => BeginDrag(button, tile, e);
        tile.MouseMove += (_, e) => ContinueDrag(e);
        tile.MouseLeftButtonUp += (_, e) => EndDrag(tile, e);

        DeckGrid.SetColumn(tile, button.Col);
        DeckGrid.SetRow(tile, button.Row);
        DeckGrid.SetColumnSpan(tile, button.ColSpan);
        DeckGrid.SetRowSpan(tile, button.RowSpan);
        return tile;
    }

    private void BeginDrag(ButtonEditModel button, Border tile, MouseButtonEventArgs e)
    {
        _dragging = button;
        _dragOrigin = e.GetPosition(this);
        _dragStarted = false;
        tile.CaptureMouse();
        e.Handled = true;
    }

    private void ContinueDrag(MouseEventArgs e)
    {
        if (_dragging is null || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var position = e.GetPosition(this);
        if (!_dragStarted
            && (Math.Abs(position.X - _dragOrigin.X) > DragThreshold
                || Math.Abs(position.Y - _dragOrigin.Y) > DragThreshold))
        {
            _dragStarted = true;
        }
    }

    private void EndDrag(Border tile, MouseButtonEventArgs e)
    {
        tile.ReleaseMouseCapture();

        var button = _dragging;
        _dragging = null;

        if (button is null)
        {
            return;
        }

        e.Handled = true;

        if (!_dragStarted)
        {
            Selected = button;
            ButtonSelected?.Invoke(this, button);
            return;
        }

        _dragStarted = false;

        if (!_grid.TryGetCell(e.GetPosition(_grid), out var column, out var row))
        {
            return;
        }

        if (column == button.Col && row == button.Row)
        {
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
}
