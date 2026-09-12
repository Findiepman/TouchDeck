using System.Windows;
using System.Windows.Controls;

namespace TouchDeck.App.Rendering;

/// <summary>The cell geometry a layout pass worked out.</summary>
/// <param name="CellWidth">Width of one cell in device independent units.</param>
/// <param name="CellHeight">Height of one cell in device independent units.</param>
public sealed record CellGeometry(double CellWidth, double CellHeight);

/// <summary>
/// Lays children out on a fixed grid of cells. Cells are square unless the profile says
/// otherwise, and the whole grid is centred, so a 5 by 3 deck on a 16 by 9 screen keeps its
/// proportions instead of stretching into rectangles.
/// </summary>
public sealed class DeckGrid : Panel
{
    /// <summary>Number of columns in the grid.</summary>
    public static readonly DependencyProperty ColumnsProperty = DependencyProperty.Register(
        nameof(Columns), typeof(int), typeof(DeckGrid),
        new FrameworkPropertyMetadata(1, FrameworkPropertyMetadataOptions.AffectsMeasure));

    /// <summary>Number of rows in the grid.</summary>
    public static readonly DependencyProperty RowsProperty = DependencyProperty.Register(
        nameof(Rows), typeof(int), typeof(DeckGrid),
        new FrameworkPropertyMetadata(1, FrameworkPropertyMetadataOptions.AffectsMeasure));

    /// <summary>Space between cells.</summary>
    public static readonly DependencyProperty GapProperty = DependencyProperty.Register(
        nameof(Gap), typeof(double), typeof(DeckGrid),
        new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsMeasure));

    /// <summary>Space between the panel edge and the grid.</summary>
    public static readonly DependencyProperty EdgePaddingProperty = DependencyProperty.Register(
        nameof(EdgePadding), typeof(double), typeof(DeckGrid),
        new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsMeasure));

    /// <summary>Cell width divided by cell height, or null for square cells.</summary>
    public static readonly DependencyProperty CellAspectProperty = DependencyProperty.Register(
        nameof(CellAspect), typeof(double?), typeof(DeckGrid),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsMeasure));

    /// <summary>Stretch cells to fill the panel rather than honouring the aspect.</summary>
    public static readonly DependencyProperty FillProperty = DependencyProperty.Register(
        nameof(Fill), typeof(bool), typeof(DeckGrid),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsMeasure));

    /// <summary>Column a child occupies.</summary>
    public static readonly DependencyProperty ColumnProperty = DependencyProperty.RegisterAttached(
        "Column", typeof(int), typeof(DeckGrid),
        new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.AffectsParentMeasure));

    /// <summary>Row a child occupies.</summary>
    public static readonly DependencyProperty RowProperty = DependencyProperty.RegisterAttached(
        "Row", typeof(int), typeof(DeckGrid),
        new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.AffectsParentMeasure));

    /// <summary>How many columns a child spans.</summary>
    public static readonly DependencyProperty ColumnSpanProperty = DependencyProperty.RegisterAttached(
        "ColumnSpan", typeof(int), typeof(DeckGrid),
        new FrameworkPropertyMetadata(1, FrameworkPropertyMetadataOptions.AffectsParentMeasure));

    /// <summary>How many rows a child spans.</summary>
    public static readonly DependencyProperty RowSpanProperty = DependencyProperty.RegisterAttached(
        "RowSpan", typeof(int), typeof(DeckGrid),
        new FrameworkPropertyMetadata(1, FrameworkPropertyMetadataOptions.AffectsParentMeasure));

    /// <summary>Raised after each layout pass with the cell size that was used.</summary>
    public event EventHandler<CellGeometry>? LayoutComputed;

    /// <summary>Number of columns in the grid.</summary>
    public int Columns
    {
        get => (int)GetValue(ColumnsProperty);
        set => SetValue(ColumnsProperty, value);
    }

    /// <summary>Number of rows in the grid.</summary>
    public int Rows
    {
        get => (int)GetValue(RowsProperty);
        set => SetValue(RowsProperty, value);
    }

    /// <summary>Space between cells.</summary>
    public double Gap
    {
        get => (double)GetValue(GapProperty);
        set => SetValue(GapProperty, value);
    }

    /// <summary>Space between the panel edge and the grid.</summary>
    public double EdgePadding
    {
        get => (double)GetValue(EdgePaddingProperty);
        set => SetValue(EdgePaddingProperty, value);
    }

    /// <summary>Cell width divided by cell height, or null for square cells.</summary>
    public double? CellAspect
    {
        get => (double?)GetValue(CellAspectProperty);
        set => SetValue(CellAspectProperty, value);
    }

    /// <summary>Stretch cells to fill the panel rather than honouring the aspect.</summary>
    public bool Fill
    {
        get => (bool)GetValue(FillProperty);
        set => SetValue(FillProperty, value);
    }

    /// <summary>Reads the column of a child.</summary>
    /// <param name="element">The child.</param>
    public static int GetColumn(UIElement element) => (int)element.GetValue(ColumnProperty);

    /// <summary>Sets the column of a child.</summary>
    /// <param name="element">The child.</param>
    /// <param name="value">Zero based column.</param>
    public static void SetColumn(UIElement element, int value) => element.SetValue(ColumnProperty, value);

    /// <summary>Reads the row of a child.</summary>
    /// <param name="element">The child.</param>
    public static int GetRow(UIElement element) => (int)element.GetValue(RowProperty);

    /// <summary>Sets the row of a child.</summary>
    /// <param name="element">The child.</param>
    /// <param name="value">Zero based row.</param>
    public static void SetRow(UIElement element, int value) => element.SetValue(RowProperty, value);

    /// <summary>Reads the column span of a child.</summary>
    /// <param name="element">The child.</param>
    public static int GetColumnSpan(UIElement element) => (int)element.GetValue(ColumnSpanProperty);

    /// <summary>Sets the column span of a child.</summary>
    /// <param name="element">The child.</param>
    /// <param name="value">Number of columns, at least one.</param>
    public static void SetColumnSpan(UIElement element, int value) => element.SetValue(ColumnSpanProperty, value);

    /// <summary>Reads the row span of a child.</summary>
    /// <param name="element">The child.</param>
    public static int GetRowSpan(UIElement element) => (int)element.GetValue(RowSpanProperty);

    /// <summary>Sets the row span of a child.</summary>
    /// <param name="element">The child.</param>
    /// <param name="value">Number of rows, at least one.</param>
    public static void SetRowSpan(UIElement element, int value) => element.SetValue(RowSpanProperty, value);

    /// <summary>
    /// Works out which cell a point falls in, using the layout from the last pass. Used by
    /// the config center to turn a click or a drop into a column and row.
    /// </summary>
    /// <param name="point">A point in this panel's coordinates.</param>
    /// <param name="column">The column the point falls in.</param>
    /// <param name="row">The row the point falls in.</param>
    /// <returns>False when the point is outside the grid.</returns>
    public bool TryGetCell(Point point, out int column, out int row)
    {
        column = 0;
        row = 0;

        var layout = Compute(RenderSize);
        if (layout.CellWidth <= 0 || layout.CellHeight <= 0)
        {
            return false;
        }

        var x = (point.X - layout.OriginX) / (layout.CellWidth + layout.Gap);
        var y = (point.Y - layout.OriginY) / (layout.CellHeight + layout.Gap);

        if (x < 0 || y < 0)
        {
            return false;
        }

        column = (int)x;
        row = (int)y;

        return column < Math.Max(1, Columns) && row < Math.Max(1, Rows);
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        var layout = Compute(availableSize);

        foreach (UIElement child in InternalChildren)
        {
            child.Measure(SizeOf(child, layout));
        }

        return availableSize;
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        var layout = Compute(finalSize);

        foreach (UIElement child in InternalChildren)
        {
            var column = Math.Max(0, GetColumn(child));
            var row = Math.Max(0, GetRow(child));
            var size = SizeOf(child, layout);

            child.Arrange(new Rect(
                new Point(
                    layout.OriginX + (column * (layout.CellWidth + layout.Gap)),
                    layout.OriginY + (row * (layout.CellHeight + layout.Gap))),
                size));
        }

        LayoutComputed?.Invoke(this, new CellGeometry(layout.CellWidth, layout.CellHeight));
        return finalSize;
    }

    private static Size SizeOf(UIElement child, Layout layout)
    {
        var columnSpan = Math.Max(1, GetColumnSpan(child));
        var rowSpan = Math.Max(1, GetRowSpan(child));

        return new Size(
            Math.Max(0, (columnSpan * layout.CellWidth) + ((columnSpan - 1) * layout.Gap)),
            Math.Max(0, (rowSpan * layout.CellHeight) + ((rowSpan - 1) * layout.Gap)));
    }

    private Layout Compute(Size available)
    {
        var columns = Math.Max(1, Columns);
        var rows = Math.Max(1, Rows);
        var gap = Math.Max(0, Gap);
        var padding = Math.Max(0, EdgePadding);

        var width = double.IsInfinity(available.Width) ? 0 : available.Width;
        var height = double.IsInfinity(available.Height) ? 0 : available.Height;

        var usableWidth = Math.Max(0, width - (2 * padding) - ((columns - 1) * gap));
        var usableHeight = Math.Max(0, height - (2 * padding) - ((rows - 1) * gap));

        var cellWidth = usableWidth / columns;
        var cellHeight = usableHeight / rows;

        if (!Fill)
        {
            var aspect = CellAspect is { } value && value > 0 ? value : 1.0;

            // Keep the requested shape and shrink to whichever axis runs out first.
            if (cellWidth / aspect <= cellHeight)
            {
                cellHeight = cellWidth / aspect;
            }
            else
            {
                cellWidth = cellHeight * aspect;
            }
        }

        var totalWidth = (columns * cellWidth) + ((columns - 1) * gap);
        var totalHeight = (rows * cellHeight) + ((rows - 1) * gap);

        return new Layout(
            cellWidth,
            cellHeight,
            gap,
            (width - totalWidth) / 2,
            (height - totalHeight) / 2);
    }

    private sealed record Layout(double CellWidth, double CellHeight, double Gap, double OriginX, double OriginY);
}
