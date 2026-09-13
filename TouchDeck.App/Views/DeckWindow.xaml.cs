using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using Serilog;
using TouchDeck.App.Rendering;
using TouchDeck.App.ViewModels;
using TouchDeck.Core.Configuration;
using TouchDeck.Platform.Display;
using TouchDeck.Platform.Input;
using TouchDeck.Platform.Windowing;

namespace TouchDeck.App.Views;

/// <summary>
/// The panel itself: a borderless, always on top, never focusable window filling one monitor.
/// </summary>
public partial class DeckWindow : Window
{
    /// <summary>
    /// How long a pending rebuild waits for a finger that is never reported as lifted. Long
    /// enough that no ordinary press reaches it, short enough that a panel which somehow
    /// loses track of a press starts drawing again by itself.
    /// </summary>
    private static readonly TimeSpan PatienceWithAHeldButton = TimeSpan.FromSeconds(2);

    private readonly DeckViewModel _viewModel;
    private readonly IconFactory _icons;
    private readonly ILogger _logger;
    private readonly SurfaceGate _gate = new();
    private readonly DispatcherTimer _impatience;

    /// <summary>Which button each finger landed on, so it is the one that gets the release.</summary>
    private readonly Dictionary<int, DeckButtonView> _fingers = new();

    private HwndSource? _source;
    private bool _tooSmallReported;

    /// <summary>Whether the first contact of each kind has been reported yet.</summary>
    private bool _touchReported;
    private bool _mouseReported;

    /// <summary>Creates the panel.</summary>
    /// <param name="viewModel">What to show.</param>
    /// <param name="icons">Where button icons and the background image are drawn from.</param>
    /// <param name="logger">Where placement and layout problems are recorded.</param>
    public DeckWindow(DeckViewModel viewModel, IconFactory icons, ILogger logger)
    {
        _viewModel = viewModel;
        _icons = icons;
        _logger = logger.ForContext<DeckWindow>();

        InitializeComponent();

        _impatience = new DispatcherTimer(DispatcherPriority.Background, Dispatcher)
        {
            Interval = PatienceWithAHeldButton,
        };

        _impatience.Tick += (_, _) =>
        {
            _impatience.Stop();

            if (_gate.GiveUp())
            {
                _logger.Warning("A button was still down after {Seconds}s, so the panel redrew anyway.",
                    PatienceWithAHeldButton.TotalSeconds);
                Refresh();
            }
        };

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

        TakeTouchInput(handle);

        Refresh();
    }

    /// <summary>
    /// Asks Windows for touch contacts rather than the mouse clicks it would otherwise
    /// synthesise. Emulation moves the pointer to wherever the finger landed, and the panel
    /// is on another screen, so every tap used to drag the pointer off the screen the user
    /// was looking at. A game steering its camera by mouse movement reads that as one
    /// enormous flick, which is what this is here to stop.
    /// </summary>
    /// <param name="handle">The panel's window handle.</param>
    private void TakeTouchInput(nint handle)
    {
        if (!_viewModel.Configuration.App.Behaviour.ClaimTouchInput)
        {
            _logger.Information("Not taking touch input, so a tap moves the mouse pointer as usual.");
            return;
        }

        if (HwndSource.FromHwnd(handle) is not { } source)
        {
            return;
        }

        if (!TouchWindow.Claim(handle, _logger))
        {
            return;
        }

        _source = source;
        source.AddHook(OnWindowMessage);

        _logger.Information("Taking touch input directly, so a tap does not move the mouse pointer.");
    }

    /// <summary>
    /// Routes a touch contact to the button under it. Having asked Windows for contacts, the
    /// panel has to deliver them itself: WPF's own touch events come from a different path
    /// that asking may well have switched off. Both are harmless together, because pressing
    /// a button that is already down does nothing.
    /// </summary>
    private nint OnWindowMessage(nint handle, int message, nint wParam, nint lParam, ref bool handled)
    {
        if (message != TouchWindow.TouchMessage)
        {
            return 0;
        }

        foreach (var contact in TouchWindow.Read(handle, (int)(wParam & 0xFFFF), lParam))
        {
            // Once, on the first contact. Which way a press arrives is the whole question
            // when the pointer is being dragged onto the panel, and it is not otherwise
            // possible to tell from outside.
            if (!_touchReported)
            {
                _touchReported = true;
                _logger.Information(
                    "First touch arrived as WM_TOUCH at {X},{Y}, so Windows is sending this " +
                    "window contacts rather than emulating a mouse.",
                    contact.X,
                    contact.Y);
            }

            switch (contact.Phase)
            {
                case TouchPhase.Down when Under(contact) is { } view:
                    _fingers[contact.Id] = view;
                    view.Press();
                    break;

                case TouchPhase.Up when _fingers.Remove(contact.Id, out var pressed):
                    // The one the finger landed on, not the one it happens to be over now,
                    // so sliding off a button still ends the press on that button.
                    pressed.Release();
                    break;
            }
        }

        handled = true;
        return 0;
    }

    /// <summary>
    /// Reports the first press that arrives as a mouse click. On a deck with no mouse
    /// plugged in that means a finger was turned into one, which is what moves the pointer
    /// and, in a game that steers by the mouse, swings the camera.
    /// </summary>
    protected override void OnPreviewMouseLeftButtonDown(System.Windows.Input.MouseButtonEventArgs e)
    {
        base.OnPreviewMouseLeftButtonDown(e);

        if (_mouseReported)
        {
            return;
        }

        _mouseReported = true;

        var at = e.GetPosition(this);
        _logger.Information(
            "First press arrived as a mouse click at {X:0},{Y:0}. If that was a finger, " +
            "Windows is still emulating a mouse for this window.",
            at.X,
            at.Y);
    }

    /// <summary>The button under a contact, or null when it landed on the gap between them.</summary>
    /// <param name="contact">Where the finger is, in the window's own pixels.</param>
    private DeckButtonView? Under(TouchContact contact)
    {
        if (_source?.CompositionTarget is not { } target)
        {
            return null;
        }

        var point = target.TransformFromDevice.Transform(new Point(contact.X, contact.Y));

        if (VisualTreeHelper.HitTest(this, point)?.VisualHit is not { } hit)
        {
            return null;
        }

        for (DependencyObject? node = hit; node is not null; node = VisualTreeHelper.GetParent(node))
        {
            if (node is DeckButtonView view)
            {
                return view;
            }
        }

        return null;
    }

    /// <inheritdoc />
    protected override void OnClosed(EventArgs e)
    {
        _impatience.Stop();

        if (_source is not null)
        {
            _source.RemoveHook(OnWindowMessage);
            TouchWindow.Release(_source.Handle);
            _source = null;
        }

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

    /// <summary>
    /// What the panel shows has changed. Rebuilding it under a finger that is still down
    /// would destroy the button that gesture belongs to, so while anything is pressed this
    /// only remembers that a rebuild is owed. See <see cref="SurfaceGate"/>.
    /// </summary>
    private void OnSurfaceChanged(object? sender, EventArgs e)
    {
        if (_gate.Changed())
        {
            Refresh();
            return;
        }

        _impatience.Stop();
        _impatience.Start();
    }

    private void Refresh()
    {
        _impatience.Stop();
        Render();
        PlaceOnTargetMonitor();
    }

    /// <summary>Rebuilds the button surface from the current profile and theme.</summary>
    private void Render()
    {
        var theme = _viewModel.Theme;
        var grid = _viewModel.Profile?.Grid ?? new GridConfig();

        Background = StyleTranslator.Brush(theme.Background);
        Backdrop.Fill = _icons.Background(theme.BackgroundImage);

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
            var view = new DeckButtonView(button, style, theme.Press, _icons);

            DeckGrid.SetColumn(view, button.Col);
            DeckGrid.SetRow(view, button.Row);
            DeckGrid.SetColumnSpan(view, button.SpanColumns);
            DeckGrid.SetRowSpan(view, button.SpanRows);

            view.Pressed += (_, config) =>
            {
                _gate.Down();
                _viewModel.Press(config);
            };

            view.Released += (_, _) =>
            {
                if (!_gate.Up())
                {
                    return;
                }

                // At background priority, so the rest of this gesture is delivered before the
                // surface it belongs to is taken away. Doing it here, inside the release,
                // would leave the tail of the gesture with nowhere to go.
                Dispatcher.BeginInvoke(new Action(Refresh), DispatcherPriority.Background);
            };

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
