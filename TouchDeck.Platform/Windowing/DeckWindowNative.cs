using Serilog;
using TouchDeck.Platform.Display;
using TouchDeck.Platform.Native;

namespace TouchDeck.Platform.Windowing;

/// <summary>
/// The window tricks the deck depends on: never taking focus, and sitting exactly on one
/// monitor whatever that monitor's scaling is.
/// </summary>
public static class DeckWindowNative
{
    /// <summary>
    /// Marks a window as never activating and as a tool window, so touching it cannot pull
    /// focus away from the application the keystroke is meant for, and so it stays out of
    /// Alt+Tab.
    /// </summary>
    /// <param name="window">The window handle.</param>
    /// <returns>True when the styles are set afterwards.</returns>
    public static bool MakeNonActivating(nint window)
    {
        var current = NativeMethods.GetWindowLongPtr(window, NativeMethods.GwlExStyle);
        var wanted = current | NativeMethods.WsExNoActivate | NativeMethods.WsExToolWindow;

        NativeMethods.SetWindowLongPtr(window, NativeMethods.GwlExStyle, wanted);

        return HasNonActivatingStyles(window);
    }

    /// <summary>Reads back the extended styles, so the focus behaviour can be asserted rather than assumed.</summary>
    /// <param name="window">The window handle.</param>
    public static bool HasNonActivatingStyles(nint window)
    {
        var style = NativeMethods.GetWindowLongPtr(window, NativeMethods.GwlExStyle);
        return (style & NativeMethods.WsExNoActivate) != 0 && (style & NativeMethods.WsExToolWindow) != 0;
    }

    /// <summary>
    /// Places a window on a monitor using physical pixels, which sidesteps having to guess
    /// which monitor's scaling the framework would have applied to a device independent size.
    /// </summary>
    /// <param name="window">The window handle.</param>
    /// <param name="left">Left edge in virtual screen pixels.</param>
    /// <param name="top">Top edge in virtual screen pixels.</param>
    /// <param name="width">Width in physical pixels.</param>
    /// <param name="height">Height in physical pixels.</param>
    /// <param name="logger">Where a placement failure is recorded.</param>
    public static void PlaceInPhysicalPixels(
        nint window,
        int left,
        int top,
        int width,
        int height,
        ILogger logger)
    {
        var placed = NativeMethods.SetWindowPos(
            window,
            NativeMethods.HwndTopmost,
            left,
            top,
            width,
            height,
            NativeMethods.SwpNoActivate | NativeMethods.SwpShowWindow);

        if (!placed)
        {
            logger.Warning(
                "Could not place the panel at {Left},{Top} {Width}x{Height} (win32 error {Error}).",
                left,
                top,
                width,
                height,
                System.Runtime.InteropServices.Marshal.GetLastWin32Error());
        }
    }

    /// <summary>Places a window so it covers a monitor exactly.</summary>
    /// <param name="window">The window handle.</param>
    /// <param name="monitor">The monitor to cover.</param>
    /// <param name="logger">Where a placement failure is recorded.</param>
    public static void FillMonitor(nint window, MonitorInfo monitor, ILogger logger) =>
        PlaceInPhysicalPixels(window, monitor.Left, monitor.Top, monitor.Width, monitor.Height, logger);
}
