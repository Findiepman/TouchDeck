using System.IO;
using System.Text.Json;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using TouchDeck.Core.Configuration;

namespace TouchDeck.App.Bootstrap;

/// <summary>
/// Sets up the rolling log file. The level follows config while the app runs; the retention
/// count is fixed when the file sink is created, so it is read before anything else happens.
/// </summary>
public sealed class LoggingSetup : IDisposable
{
    private const string OutputTemplate =
        "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}";

    private readonly LoggingLevelSwitch _levelSwitch = new(LogEventLevel.Information);

    private Logger? _logger;

    /// <summary>
    /// Reads just the logging block, before the logger exists and therefore without being
    /// able to report a problem. Anything unreadable falls back to the defaults.
    /// </summary>
    /// <param name="paths">Where config.json is.</param>
    public static LoggingConfig Peek(ConfigPaths paths)
    {
        try
        {
            if (!File.Exists(paths.ConfigFile))
            {
                return new LoggingConfig();
            }

            var json = File.ReadAllText(paths.ConfigFile);
            return JsonSerializer.Deserialize<AppConfig>(json, ConfigJson.Options)?.Logging ?? new LoggingConfig();
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return new LoggingConfig();
        }
    }

    /// <summary>Creates the logger writing into the config folder's logs directory.</summary>
    /// <param name="paths">Where the logs directory is.</param>
    /// <param name="logging">Level and retention to start with.</param>
    public ILogger Start(ConfigPaths paths, LoggingConfig logging)
    {
        Directory.CreateDirectory(paths.LogsDirectory);
        Apply(logging);

        _logger = new LoggerConfiguration()
            .MinimumLevel.ControlledBy(_levelSwitch)
            .WriteTo.File(
                Path.Combine(paths.LogsDirectory, "touchdeck-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: Math.Max(1, logging.RetainDays),
                shared: true,
                outputTemplate: OutputTemplate)
            .CreateLogger();

        Log.Logger = _logger;
        return _logger;
    }

    /// <summary>Applies the level from a freshly loaded configuration.</summary>
    /// <param name="logging">The logging block from config.json.</param>
    public void Apply(LoggingConfig logging) =>
        _levelSwitch.MinimumLevel = logging.Level switch
        {
            LogLevel.Verbose => LogEventLevel.Verbose,
            LogLevel.Debug => LogEventLevel.Debug,
            LogLevel.Warning => LogEventLevel.Warning,
            LogLevel.Error => LogEventLevel.Error,
            LogLevel.Fatal => LogEventLevel.Fatal,
            _ => LogEventLevel.Information,
        };

    /// <inheritdoc />
    public void Dispose()
    {
        _logger?.Dispose();
        _logger = null;
    }
}
