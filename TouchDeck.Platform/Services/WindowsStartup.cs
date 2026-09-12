using Microsoft.Win32;
using Serilog;

namespace TouchDeck.Platform.Services;

/// <summary>
/// Starts the deck when Windows starts, through the per user Run key. No administrator
/// rights, no scheduled task, and the user can see and remove it in Task Manager's startup
/// tab like anything else.
/// </summary>
public sealed class WindowsStartup
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    /// <summary>The name the entry appears under, in the registry and in Task Manager.</summary>
    public const string EntryName = "TouchDeck";

    private readonly ILogger _logger;

    /// <summary>Creates the registration.</summary>
    /// <param name="logger">Where changes are recorded.</param>
    public WindowsStartup(ILogger logger)
    {
        _logger = logger.ForContext<WindowsStartup>();
    }

    /// <summary>What Windows would run at login, or null when there is no entry.</summary>
    public string? CurrentCommand
    {
        get
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
                return key?.GetValue(EntryName) as string;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
            {
                _logger.Warning(ex, "Could not read the Windows startup list.");
                return null;
            }
        }
    }

    /// <summary>
    /// Adds or removes the entry so it matches what the config asks for. Safe to call on
    /// every start and every reload, and rewrites the path when the deck has moved.
    /// </summary>
    /// <param name="enabled">Whether the deck should start with Windows.</param>
    /// <param name="command">The command line to register, already quoted where needed.</param>
    /// <returns>True when something was changed.</returns>
    public bool Apply(bool enabled, string command)
    {
        var current = CurrentCommand;

        if (enabled && string.Equals(current, command, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!enabled && current is null)
        {
            return false;
        }

        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);

            if (key is null)
            {
                _logger.Warning("Windows would not open the startup list, so it was left alone.");
                return false;
            }

            if (enabled)
            {
                key.SetValue(EntryName, command, RegistryValueKind.String);
                _logger.Information("TouchDeck will start with Windows: {Command}", command);
            }
            else
            {
                key.DeleteValue(EntryName, throwOnMissingValue: false);
                _logger.Information("TouchDeck will no longer start with Windows.");
            }

            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            _logger.Warning(ex, "Could not change whether TouchDeck starts with Windows.");
            return false;
        }
    }

    /// <summary>
    /// The command line that should be registered for a running deck, quoted so a path with
    /// spaces in it survives.
    /// </summary>
    /// <param name="executablePath">Where this copy of the deck lives.</param>
    /// <param name="configDirectory">A non default config folder, or null for the usual one.</param>
    public static string CommandFor(string executablePath, string? configDirectory) =>
        configDirectory is { Length: > 0 }
            ? $"\"{executablePath}\" --config \"{configDirectory}\""
            : $"\"{executablePath}\"";
}
