using System.ComponentModel;
using System.Diagnostics;
using Serilog;
using TouchDeck.Core.Abstractions;
using TouchDeck.Platform.Windowing;

namespace TouchDeck.Platform.Process;

/// <summary>
/// Starts programs, documents and shell targets. Uses the shell so that things like
/// <c>ms-settings:</c> and file associations work the same way they do from Run.
/// </summary>
public sealed class ShellProcessLauncher : IProcessLauncher
{
    private readonly ILogger _logger;
    private readonly WindowManager _windows;

    /// <summary>Creates a launcher.</summary>
    /// <param name="logger">Where launches are recorded.</param>
    /// <param name="windows">Used to bring an already running copy forward.</param>
    public ShellProcessLauncher(ILogger logger, WindowManager windows)
    {
        _logger = logger.ForContext<ShellProcessLauncher>();
        _windows = windows;
    }

    /// <inheritdoc />
    public void Launch(LaunchRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Path))
        {
            throw new ArgumentException("A launch needs a path.", nameof(request));
        }

        var path = Environment.ExpandEnvironmentVariables(request.Path);

        if (request.FocusIfRunning || request.SingleInstance)
        {
            var name = System.IO.Path.GetFileNameWithoutExtension(path);

            if (IsRunning(name))
            {
                if (request.FocusIfRunning && _windows.Find(new WindowMatch(name, null)) is { } window)
                {
                    WindowManager.Focus(window);
                    _logger.Information("Brought {Name} forward instead of starting another.", name);
                    return;
                }

                if (request.SingleInstance)
                {
                    _logger.Information("{Name} is already running, so nothing was started.", name);
                    return;
                }
            }
        }

        var info = new ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = true,
        };

        if (!string.IsNullOrWhiteSpace(request.Arguments))
        {
            info.Arguments = Environment.ExpandEnvironmentVariables(request.Arguments);
        }

        if (!string.IsNullOrWhiteSpace(request.WorkingDirectory))
        {
            info.WorkingDirectory = Environment.ExpandEnvironmentVariables(request.WorkingDirectory);
        }

        try
        {
            using var started = global::System.Diagnostics.Process.Start(info);
            _logger.Information("Launched {Path} {Arguments}", info.FileName, info.Arguments);
        }
        catch (Win32Exception ex)
        {
            throw new InvalidOperationException($"Windows could not start \"{info.FileName}\": {ex.Message}", ex);
        }
        catch (FileNotFoundException ex)
        {
            throw new InvalidOperationException($"\"{info.FileName}\" does not exist.", ex);
        }
    }

    private static bool IsRunning(string processName)
    {
        try
        {
            return global::System.Diagnostics.Process.GetProcessesByName(processName).Length > 0;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }
}
