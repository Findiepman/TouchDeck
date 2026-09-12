using SystemProcess = System.Diagnostics.Process;
using System.Diagnostics;
using Serilog;
using TouchDeck.Core.Abstractions;

namespace TouchDeck.Platform.Services;

/// <summary>Runs a command line through PowerShell or the classic command processor.</summary>
public sealed class ShellRunner : IShellRunner
{
    private readonly ILogger _logger;

    /// <summary>Creates a shell runner.</summary>
    /// <param name="logger">Where commands are recorded.</param>
    public ShellRunner(ILogger logger)
    {
        _logger = logger.ForContext<ShellRunner>();
    }

    /// <inheritdoc />
    public async Task<string> RunAsync(
        string command,
        ShellKind shell,
        bool hidden,
        bool captureOutput,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            return string.Empty;
        }

        var info = new ProcessStartInfo
        {
            FileName = shell == ShellKind.Cmd ? "cmd.exe" : "powershell.exe",
            CreateNoWindow = hidden,
            UseShellExecute = false,
            RedirectStandardOutput = captureOutput,
            RedirectStandardError = captureOutput,
            WindowStyle = hidden ? ProcessWindowStyle.Hidden : ProcessWindowStyle.Normal,
        };

        if (shell == ShellKind.Cmd)
        {
            info.ArgumentList.Add("/c");
            info.ArgumentList.Add(command);
        }
        else
        {
            info.ArgumentList.Add("-NoProfile");
            info.ArgumentList.Add("-NonInteractive");
            info.ArgumentList.Add("-Command");
            info.ArgumentList.Add(command);
        }

        using var process = SystemProcess.Start(info)
            ?? throw new InvalidOperationException($"Windows would not start {info.FileName}.");

        _logger.Information("Ran {Shell}: {Command}", info.FileName, command);

        if (!captureOutput)
        {
            // Fire and forget, so a long running command does not hold a button down.
            return string.Empty;
        }

        var output = await process.StandardOutput.ReadToEndAsync(ct).ConfigureAwait(false);
        var errors = await process.StandardError.ReadToEndAsync(ct).ConfigureAwait(false);

        await process.WaitForExitAsync(ct).ConfigureAwait(false);

        if (process.ExitCode != 0 && errors.Length > 0)
        {
            _logger.Warning("The command exited {Code}: {Errors}", process.ExitCode, errors.Trim());
        }

        return (output.Length > 0 ? output : errors).Trim();
    }
}
