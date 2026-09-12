using TouchDeck.Core.Configuration;
using TouchDeck.Platform.Native;

namespace TouchDeck.Platform.Display;

/// <summary>The monitor a <see cref="DisplayConfig"/> resolved to, and how it was found.</summary>
/// <param name="Monitor">The chosen monitor, or null when nothing matched and no fallback existed.</param>
/// <param name="Warning">Set when the first choice was unavailable, for the log and the overlay.</param>
public sealed record MonitorSelection(MonitorInfo? Monitor, string? Warning);

/// <summary>
/// Enumerates monitors and resolves <see cref="DisplayConfig"/> against them. Selection
/// falls back rather than failing, so a touchscreen that moved never leaves a blank panel.
/// </summary>
public static class MonitorLocator
{
    /// <summary>
    /// Lists every monitor. Index zero is the primary; the rest follow left to right, top to
    /// bottom, so indices stay meaningful between runs.
    /// </summary>
    public static IReadOnlyList<MonitorInfo> Enumerate()
    {
        var found = new List<(string Device, bool Primary, NativeMethods.Rect Bounds, uint DpiX, uint DpiY)>();

        bool Callback(nint monitor, nint dc, ref NativeMethods.Rect rect, nint data)
        {
            var info = new NativeMethods.MonitorInfoEx
            {
                Size = System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.MonitorInfoEx>(),
                DeviceName = string.Empty,
            };

            if (!NativeMethods.GetMonitorInfoW(monitor, ref info))
            {
                return true;
            }

            uint dpiX = (uint)MonitorInfo.DefaultDpi;
            uint dpiY = (uint)MonitorInfo.DefaultDpi;

            // Fails only on the shell being unavailable, in which case 96 is the right answer.
            if (NativeMethods.GetDpiForMonitor(monitor, NativeMethods.MdtEffectiveDpi, out var x, out var y) == 0)
            {
                dpiX = x;
                dpiY = y;
            }

            found.Add((
                info.DeviceName,
                (info.Flags & NativeMethods.MonitorInfoPrimary) != 0,
                info.Monitor,
                dpiX,
                dpiY));

            return true;
        }

        // Held in a local so the delegate cannot be collected while Windows is calling it.
        NativeMethods.MonitorEnumProc callback = Callback;
        NativeMethods.EnumDisplayMonitors(0, 0, callback, 0);
        GC.KeepAlive(callback);

        return found
            .OrderByDescending(m => m.Primary)
            .ThenBy(m => m.Bounds.Left)
            .ThenBy(m => m.Bounds.Top)
            .Select((m, index) => new MonitorInfo(
                index,
                m.Device,
                m.Primary,
                m.Bounds.Left,
                m.Bounds.Top,
                m.Bounds.Width,
                m.Bounds.Height,
                m.DpiX,
                m.DpiY))
            .ToArray();
    }

    /// <summary>Resolves a display configuration against the monitors currently attached.</summary>
    /// <param name="display">What the user asked for.</param>
    /// <param name="monitors">The monitors to choose from, or null to enumerate them now.</param>
    public static MonitorSelection Select(DisplayConfig display, IReadOnlyList<MonitorInfo>? monitors = null)
    {
        monitors ??= Enumerate();

        if (monitors.Count == 0)
        {
            return new MonitorSelection(null, "Windows reports no monitors at all.");
        }

        var match = FindMatch(display, monitors);
        if (match is not null)
        {
            return new MonitorSelection(match, null);
        }

        var asked = Describe(display);

        if (display.FallbackToIndex is { } fallbackIndex
            && monitors.FirstOrDefault(m => m.Index == fallbackIndex) is { } fallback)
        {
            return new MonitorSelection(
                fallback,
                $"No monitor matched {asked}, so the deck is on the fallback monitor {fallback}.");
        }

        return new MonitorSelection(
            null,
            $"No monitor matched {asked} and display.fallbackToIndex names no attached monitor.");
    }

    private static MonitorInfo? FindMatch(DisplayConfig display, IReadOnlyList<MonitorInfo> monitors) =>
        display.Select switch
        {
            DisplaySelect.Primary => monitors.FirstOrDefault(m => m.IsPrimary) ?? monitors[0],
            DisplaySelect.ByName => monitors.FirstOrDefault(
                m => string.Equals(m.DeviceName, display.Value, StringComparison.OrdinalIgnoreCase)),
            DisplaySelect.ByResolution => monitors.FirstOrDefault(
                m => string.Equals(m.Resolution, display.Value?.Trim(), StringComparison.OrdinalIgnoreCase)),
            DisplaySelect.ByIndex => int.TryParse(display.Value, out var index)
                ? monitors.FirstOrDefault(m => m.Index == index)
                : null,
            _ => null,
        };

    private static string Describe(DisplayConfig display) => display.Select switch
    {
        DisplaySelect.ByName => $"the device name \"{display.Value}\"",
        DisplaySelect.ByIndex => $"monitor index {display.Value}",
        DisplaySelect.ByResolution => $"a monitor at {display.Value}",
        _ => "the requested monitor",
    };
}
