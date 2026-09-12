using System.Text;
using System.Text.RegularExpressions;
using Serilog;
using TouchDeck.Core.Abstractions;
using TouchDeck.Platform.Native;

namespace TouchDeck.Platform.Windowing;

/// <summary>Finds other applications' windows and does something to them.</summary>
public sealed class WindowManager : IWindowManager
{
    private readonly ILogger _logger;

    /// <summary>Creates a window manager.</summary>
    /// <param name="logger">Where failures are recorded.</param>
    public WindowManager(ILogger logger)
    {
        _logger = logger.ForContext<WindowManager>();
    }

    /// <inheritdoc />
    public string? ForegroundProcessName
    {
        get
        {
            var window = NativeMethods.GetForegroundWindow();
            return window == 0 ? null : ProcessNameOf(window);
        }
    }

    /// <inheritdoc />
    public bool Apply(WindowOperation operation, WindowMatch match)
    {
        if (Find(match) is not { } window)
        {
            return false;
        }

        switch (operation)
        {
            case WindowOperation.Focus:
                Focus(window);
                break;
            case WindowOperation.Minimise:
                NativeMethods.ShowWindow(window, NativeMethods.SwMinimise);
                break;
            case WindowOperation.Maximise:
                NativeMethods.ShowWindow(window, NativeMethods.SwMaximise);
                break;
            case WindowOperation.Restore:
                NativeMethods.ShowWindow(window, NativeMethods.SwRestore);
                break;
            case WindowOperation.Close:
                NativeMethods.PostMessageW(window, NativeMethods.WmClose, 0, 0);
                break;
        }

        return true;
    }

    /// <summary>
    /// Brings a window forward. Windows refuses this to a process that does not have the
    /// foreground, so the deck borrows the current foreground thread's input state first.
    /// </summary>
    /// <param name="window">The window to bring forward.</param>
    public static void Focus(nint window)
    {
        if (NativeMethods.IsIconic(window))
        {
            NativeMethods.ShowWindow(window, NativeMethods.SwRestore);
        }

        var foreground = NativeMethods.GetForegroundWindow();

        if (foreground == window)
        {
            return;
        }

        var us = NativeMethods.GetCurrentThreadId();
        var them = NativeMethods.GetWindowThreadProcessId(foreground, out _);

        if (them != 0 && them != us)
        {
            NativeMethods.AttachThreadInput(us, them, true);
            NativeMethods.SetForegroundWindow(window);
            NativeMethods.AttachThreadInput(us, them, false);
            return;
        }

        NativeMethods.SetForegroundWindow(window);
    }

    /// <summary>Finds the first visible top level window matching.</summary>
    /// <param name="match">What to look for.</param>
    /// <returns>The window handle, or null when nothing matched.</returns>
    public nint? Find(WindowMatch match)
    {
        if (string.IsNullOrWhiteSpace(match.ProcessName) && string.IsNullOrWhiteSpace(match.TitleRegex))
        {
            return null;
        }

        Regex? title = null;

        if (match.TitleRegex is { Length: > 0 } pattern)
        {
            try
            {
                title = new Regex(pattern, RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(200));
            }
            catch (ArgumentException ex)
            {
                _logger.Warning("\"{Pattern}\" is not a valid title expression: {Reason}", pattern, ex.Message);
                return null;
            }
        }

        var wanted = Normalise(match.ProcessName);
        nint found = 0;

        bool Visit(nint window, nint data)
        {
            if (!NativeMethods.IsWindowVisible(window))
            {
                return true;
            }

            if (wanted is not null && !string.Equals(ProcessNameOf(window), wanted, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (title is not null)
            {
                var text = TitleOf(window);

                if (text.Length == 0 || !SafeMatch(title, text))
                {
                    return true;
                }
            }
            else if (wanted is not null && TitleOf(window).Length == 0)
            {
                // A process has many invisible helper windows. The one with a title is the one
                // a person means.
                return true;
            }

            found = window;
            return false;
        }

        NativeMethods.EnumWindowsProc callback = Visit;
        NativeMethods.EnumWindows(callback, 0);
        GC.KeepAlive(callback);

        return found == 0 ? null : found;
    }

    private bool SafeMatch(Regex regex, string text)
    {
        try
        {
            return regex.IsMatch(text);
        }
        catch (RegexMatchTimeoutException)
        {
            _logger.Warning("A window title expression took too long and was abandoned.");
            return false;
        }
    }

    private static string? Normalise(string? processName) =>
        string.IsNullOrWhiteSpace(processName)
            ? null
            : processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                ? processName[..^4]
                : processName;

    private static string TitleOf(nint window)
    {
        var length = NativeMethods.GetWindowTextLengthW(window);

        if (length <= 0)
        {
            return string.Empty;
        }

        var text = new StringBuilder(length + 1);
        NativeMethods.GetWindowTextW(window, text, text.Capacity);
        return text.ToString();
    }

    private static string? ProcessNameOf(nint window)
    {
        NativeMethods.GetWindowThreadProcessId(window, out var processId);

        if (processId == 0)
        {
            return null;
        }

        try
        {
            using var process = global::System.Diagnostics.Process.GetProcessById((int)processId);
            return process.ProcessName;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return null;
        }
    }
}
