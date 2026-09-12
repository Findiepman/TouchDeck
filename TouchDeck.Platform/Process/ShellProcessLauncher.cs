using System.ComponentModel;
using System.Diagnostics;
using Serilog;
using TouchDeck.Core.Abstractions;

namespace TouchDeck.Platform.Process;

/// <summary>
/// Starts programs, documents and shell targets. Uses the shell so that things like
/// <c>ms-settings:</c> and file associations work the same way they do from Run.
/// </summary>
public sealed class ShellProcessLauncher : IProcessLauncher
{
    private readonly ILogger _logger;

    /// <summary>Creates a launcher.</summary>
    /// <param name="logger">Where launches are recorded.</param>
    public ShellProcessLauncher(ILogger logger)
    {
        _logger = logger.ForContext<ShellProcessLauncher>();
    }

    /// <inheritdoc />
    public void Launch(LaunchRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Path))
        {
            throw new ArgumentException("A launch needs a path.", nameof(request));
        }

        var info = new ProcessStartInfo
        {
            FileName = Environment.ExpandEnvironmentVariables(request.Path),
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
            using var started = System.Diagnostics.Process.Start(info);
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
}
