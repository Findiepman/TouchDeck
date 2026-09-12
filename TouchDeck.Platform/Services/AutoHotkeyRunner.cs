using SystemProcess = System.Diagnostics.Process;
using System.Diagnostics;
using Serilog;
using TouchDeck.Core.Abstractions;

namespace TouchDeck.Platform.Services;

/// <summary>
/// Runs a snippet of AutoHotkey v2, which is the way to do something the deck has no action
/// for without recompiling it. Does nothing useful unless AutoHotkey is installed.
/// </summary>
public sealed class AutoHotkeyRunner : IScriptRunner
{
    private static readonly string[] LikelyPaths =
    {
        @"%ProgramFiles%\AutoHotkey\v2\AutoHotkey64.exe",
        @"%ProgramFiles%\AutoHotkey\v2\AutoHotkey.exe",
        @"%ProgramFiles%\AutoHotkey\AutoHotkey.exe",
        @"%LocalAppData%\Programs\AutoHotkey\v2\AutoHotkey64.exe",
        @"%LocalAppData%\Programs\AutoHotkey\v2\AutoHotkey.exe",
    };

    private readonly ILogger _logger;
    private readonly Lazy<string?> _executable;

    /// <summary>Creates a runner.</summary>
    /// <param name="logger">Where scripts are recorded.</param>
    public AutoHotkeyRunner(ILogger logger)
    {
        _logger = logger.ForContext<AutoHotkeyRunner>();
        _executable = new Lazy<string?>(FindAutoHotkey);
    }

    /// <inheritdoc />
    public bool IsAutoHotkeyInstalled => _executable.Value is not null;

    /// <inheritdoc />
    public async Task RunAutoHotkeyAsync(string script, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(script))
        {
            return;
        }

        if (_executable.Value is not { } executable)
        {
            throw new InvalidOperationException(
                "AutoHotkey is not installed, so this button cannot run its script. " +
                "Install AutoHotkey v2 from autohotkey.com.");
        }

        // AutoHotkey runs files, not strings, so the snippet goes to a temporary one.
        var file = Path.Combine(Path.GetTempPath(), $"touchdeck-{Guid.NewGuid():N}.ahk");
        await File.WriteAllTextAsync(file, script, ct).ConfigureAwait(false);

        try
        {
            var info = new ProcessStartInfo(executable)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            info.ArgumentList.Add(file);

            using var process = SystemProcess.Start(info)
                ?? throw new InvalidOperationException("Windows would not start AutoHotkey.");

            _logger.Information("Running an AutoHotkey snippet through {Executable}", executable);
            await process.WaitForExitAsync(ct).ConfigureAwait(false);

            if (process.ExitCode != 0)
            {
                _logger.Warning("The AutoHotkey snippet exited {Code}.", process.ExitCode);
            }
        }
        finally
        {
            TryDelete(file);
        }
    }

    private void TryDelete(string file)
    {
        try
        {
            File.Delete(file);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.Debug("Left a temporary script behind at {File}.", file);
        }
    }

    private string? FindAutoHotkey()
    {
        foreach (var candidate in LikelyPaths)
        {
            var path = Environment.ExpandEnvironmentVariables(candidate);

            if (File.Exists(path))
            {
                return path;
            }
        }

        // Fall back to whatever is on the path.
        var onPath = Environment.GetEnvironmentVariable("PATH")?.Split(';') ?? Array.Empty<string>();

        foreach (var directory in onPath)
        {
            if (string.IsNullOrWhiteSpace(directory))
            {
                continue;
            }

            foreach (var name in new[] { "AutoHotkey64.exe", "AutoHotkey.exe" })
            {
                try
                {
                    var path = Path.Combine(directory.Trim(), name);

                    if (File.Exists(path))
                    {
                        return path;
                    }
                }
                catch (ArgumentException)
                {
                    // A malformed PATH entry is not worth failing over.
                }
            }
        }

        return null;
    }
}
