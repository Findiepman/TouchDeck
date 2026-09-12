using System.Windows;
using System.Windows.Interop;
using Microsoft.Win32;
using Serilog;
using TouchDeck.App.Rendering;
using TouchDeck.App.ViewModels;
using TouchDeck.Core.Configuration;
using TouchDeck.Platform.Display;
using TouchDeck.Platform.Windowing;

namespace TouchDeck.App.Views;

/// <summary>
/// The panel itself: a borderless, always on top, never focusable window filling one monitor.
/// </summary>
public partial class DeckWindow : Window
{
    private readonly DeckViewModel _viewModel;
    private readonly ILogger _logger;

    private bool _tooSmallReported;

    /// <summary>Creates the panel.</summary>
    /// <param name="viewModel">What to show.</param>
    /// <param name="logger">Where placement and layout problems are recorded.</param>
    public DeckWindow(DeckViewModel viewModel, ILogger logger)
    {
        _viewModel = viewModel;
        _logger = logger.ForContext<DeckWindow>();

        InitializeComponent();

        Surface.LayoutComputed += OnLayoutComputed;
        _viewModel.SurfaceChanged += OnSurfaceChanged;
        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
    }

    /// <summary>Applies the non activating styles and puts the window on its monitor.</summary>
    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        var handle = new WindowInteropHelper(this).Handle;

        if (DeckWindowNative.MakeNonActivating(handle))
        {
            _logger.Information("Panel is marked no activate and tool window, so it cannot take focus.");
        }
        else
        {
            _logger.Error("The panel could not be marked no activate. Presses may steal focus.");
        }

        Render();
        PlaceOnTargetMonitor();
    }

    /// <inheritdoc />
    protected override void OnClosed(EventArgs e)
    {
        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        _viewModel.SurfaceChanged -= OnSurfaceChanged;
        Surface.LayoutComputed -= OnLayoutComputed;
        base.OnClosed(e);
    }

    /// <summary>Keeps the panel exactly on its monitor when that monitor's scaling changes.</summary>
    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        PlaceOnTargetMonitor();
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e) =>
        Dispatcher.BeginInvoke(() => PlaceOnTargetMonitor());

    private void OnSurfaceChanged(object? sender, EventArgs e)
    {
        Render();
        PlaceOnTargetMonitor();
    }

    /// <summary>Rebuilds the button surface from the current profile and theme.</summary>
    private void Render()
    {
        var theme = _viewModel.Theme;
        var grid = _viewModel.Profile?.Grid ?? new GridConfig();

        Background = StyleTranslator.Brush(theme.Background);

        Surface.Children.Clear();
        Surface.Columns = grid.Columns;
        Surface.Rows = grid.Rows;
        Surface.Gap = theme.Gap;
        Surface.EdgePadding = theme.Padding;
        Surface.CellAspect = grid.CellAspect;
        Surface.Fill = grid.Fill;

        _tooSmallReported = false;

        foreach (var button in _viewModel.Buttons)
        {
            var style = theme.Button.With(button.Style);
            var view = new DeckButtonView(button, style, theme.Press);

            DeckGrid.SetColumn(view, button.Col);
            DeckGrid.SetRow(view, button.Row);
            DeckGrid.SetColumnSpan(view, button.SpanColumns);
            DeckGrid.SetRowSpan(view, button.SpanRows);

            view.Pressed += (_, config) => _viewModel.Press(config);

            Surface.Children.Add(view);
        }

        _logger.Debug("Rendered {Count} buttons.", Surface.Children.Count);
    }

    private void PlaceOnTargetMonitor()
    {
        var selection = MonitorLocator.Select(_viewModel.Configuration.App.Display);

        if (selection.Warning is { } warning)
        {
            _logger.Warning("{Warning}", warning);
        }

        if (selection.Monitor is not { } monitor)
        {
            _logger.Error("No monitor is available for the panel, so it stays where it is.");
            return;
        }

        var handle = new WindowInteropHelper(this).Handle;
        if (handle == 0)
        {
            return;
        }

        var display = _viewModel.Configuration.App.Display;

        if (display.Fullscreen || display.Bounds is null)
        {
            DeckWindowNative.FillMonitor(handle, monitor, _logger);
        }
        else
        {
            // Bounds are written in device independent units relative to the monitor, so they
            // scale with that monitor rather than the primary one.
            DeckWindowNative.PlaceInPhysicalPixels(
                handle,
                monitor.Left + (int)Math.Round(display.Bounds.X * monitor.ScaleX),
                monitor.Top + (int)Math.Round(display.Bounds.Y * monitor.ScaleY),
                (int)Math.Round(display.Bounds.Width * monitor.ScaleX),
                (int)Math.Round(display.Bounds.Height * monitor.ScaleY),
                _logger);
        }

        _logger.Information("Panel placed on {Monitor}", monitor);
    }

    private void OnLayoutComputed(object? sender, CellGeometry geometry)
    {
        if (_tooSmallReported)
        {
            return;
        }

        var smallest = Math.Min(geometry.CellWidth, geometry.CellHeight);
        if (smallest <= 0 || smallest >= DeckLimits.MinimumTouchTargetDip)
        {
            return;
        }

        _tooSmallReported = true;
        _logger.Warning(
            "Cells are {Size:0} units across, below the {Minimum:0} unit touch target. " +
            "Use fewer columns or rows, or a smaller gap.",
            smallest,
            DeckLimits.MinimumTouchTargetDip);
    }
}
