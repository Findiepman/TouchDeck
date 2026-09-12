namespace TouchDeck.Platform.Display;

/// <summary>
/// One monitor as Windows sees it. Bounds are physical pixels; the scale factors let a
/// caller convert them using this monitor's own dpi rather than the primary monitor's.
/// </summary>
/// <param name="Index">Position in the enumerated list. Zero is always the primary monitor.</param>
/// <param name="DeviceName">Stable Windows device name, for example <c>\\.\DISPLAY2</c>.</param>
/// <param name="IsPrimary">Whether Windows treats this as the primary monitor.</param>
/// <param name="Left">Left edge in virtual screen coordinates, in physical pixels.</param>
/// <param name="Top">Top edge in virtual screen coordinates, in physical pixels.</param>
/// <param name="Width">Width in physical pixels.</param>
/// <param name="Height">Height in physical pixels.</param>
/// <param name="DpiX">Effective horizontal dpi of this monitor.</param>
/// <param name="DpiY">Effective vertical dpi of this monitor.</param>
public sealed record MonitorInfo(
    int Index,
    string DeviceName,
    bool IsPrimary,
    int Left,
    int Top,
    int Width,
    int Height,
    uint DpiX,
    uint DpiY)
{
    /// <summary>The dpi Windows treats as unscaled.</summary>
    public const double DefaultDpi = 96.0;

    /// <summary>Horizontal scale factor of this monitor, where 1.0 is 100 percent.</summary>
    public double ScaleX => DpiX / DefaultDpi;

    /// <summary>Vertical scale factor of this monitor, where 1.0 is 100 percent.</summary>
    public double ScaleY => DpiY / DefaultDpi;

    /// <summary>Width in device independent units, using this monitor's own dpi.</summary>
    public double WidthDip => Width / ScaleX;

    /// <summary>Height in device independent units, using this monitor's own dpi.</summary>
    public double HeightDip => Height / ScaleY;

    /// <summary>Resolution as written in <c>display.value</c> when selecting by resolution.</summary>
    public string Resolution => $"{Width}x{Height}";

    /// <summary>Describes the monitor for logs and the tray menu.</summary>
    public override string ToString() =>
        $"[{Index}] {DeviceName} {Width}x{Height} at {Left},{Top} " +
        $"({DpiX} dpi{(IsPrimary ? ", primary" : string.Empty)})";
}
