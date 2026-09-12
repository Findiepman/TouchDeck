using Serilog;

namespace TouchDeck.Core.Configuration;

/// <summary>
/// Owns the live configuration. It reloads on save, and when a reload fails it keeps the
/// last good configuration running and publishes the errors so the panel can show them.
/// </summary>
public sealed class ConfigService : IDisposable
{
    private readonly ConfigPaths _paths;
    private readonly ConfigLoader _loader;
    private readonly ILogger _logger;
    private readonly TimeSpan _debounce;
    private readonly Lock _gate = new();

    private FileSystemWatcher? _watcher;
    private Timer? _debounceTimer;
    private DeckConfiguration _lastGood = new();
    private bool _disposed;

    /// <summary>Creates a config service.</summary>
    /// <param name="paths">Where the config lives.</param>
    /// <param name="loader">Reads the config files.</param>
    /// <param name="logger">Where reloads are recorded.</param>
    /// <param name="debounce">How long to wait after the last file change before reloading.</param>
    public ConfigService(ConfigPaths paths, ConfigLoader loader, ILogger logger, TimeSpan? debounce = null)
    {
        _paths = paths;
        _loader = loader;
        _logger = logger.ForContext<ConfigService>();
        _debounce = debounce ?? TimeSpan.FromMilliseconds(250);
    }

    /// <summary>Raised after a successful or failed reload, always off the UI thread.</summary>
    public event EventHandler<DeckConfiguration>? Changed;

    /// <summary>
    /// The configuration in force. After a failed reload this is the last good configuration
    /// carrying the new error messages.
    /// </summary>
    public DeckConfiguration Current
    {
        get
        {
            lock (_gate)
            {
                return _lastGood;
            }
        }
    }

    /// <summary>Performs the first load and returns the result.</summary>
    public DeckConfiguration Load()
    {
        var loaded = _loader.Load();

        lock (_gate)
        {
            _lastGood = loaded;
        }

        LogOutcome(loaded, firstLoad: true);
        return loaded;
    }

    /// <summary>Starts watching the config directory. Does nothing if already watching.</summary>
    public void StartWatching()
    {
        if (_watcher is not null || _disposed)
        {
            return;
        }

        Directory.CreateDirectory(_paths.Root);

        _watcher = new FileSystemWatcher(_paths.Root)
        {
            Filter = "*.json",
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
            EnableRaisingEvents = true,
        };

        _watcher.Changed += OnFileEvent;
        _watcher.Created += OnFileEvent;
        _watcher.Deleted += OnFileEvent;
        _watcher.Renamed += OnFileEvent;
        _watcher.Error += OnWatcherError;

        _logger.Information("Watching {Root} for config changes.", _paths.Root);
    }

    /// <summary>Stops watching, keeping the current configuration in place.</summary>
    public void StopWatching()
    {
        if (_watcher is null)
        {
            return;
        }

        _watcher.EnableRaisingEvents = false;
        _watcher.Changed -= OnFileEvent;
        _watcher.Created -= OnFileEvent;
        _watcher.Deleted -= OnFileEvent;
        _watcher.Renamed -= OnFileEvent;
        _watcher.Error -= OnWatcherError;
        _watcher.Dispose();
        _watcher = null;
    }

    /// <summary>Reloads now, bypassing the debounce.</summary>
    public void ReloadNow() => Reload();

    /// <inheritdoc />
    public void Dispose()
    {
        _disposed = true;
        StopWatching();
        _debounceTimer?.Dispose();
        _debounceTimer = null;
    }

    private void OnFileEvent(object sender, FileSystemEventArgs e)
    {
        if (_disposed)
        {
            return;
        }

        lock (_gate)
        {
            _debounceTimer ??= new Timer(_ => Reload(), null, System.Threading.Timeout.Infinite, System.Threading.Timeout.Infinite);
            _debounceTimer.Change(_debounce, System.Threading.Timeout.InfiniteTimeSpan);
        }
    }

    private void OnWatcherError(object sender, ErrorEventArgs e) =>
        _logger.Warning(e.GetException(), "The config watcher failed. Hot reload may have stopped.");

    private void Reload()
    {
        if (_disposed)
        {
            return;
        }

        DeckConfiguration published;

        try
        {
            var loaded = _loader.Load();

            lock (_gate)
            {
                published = loaded.HasErrors
                    ? _lastGood with { Messages = loaded.Messages }
                    : loaded;
                _lastGood = published;
            }

            LogOutcome(loaded, firstLoad: false);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Reloading the config threw. The previous config stays in force.");

            lock (_gate)
            {
                published = _lastGood with
                {
                    Messages = new[]
                    {
                        ValidationMessage.Error(_paths.Describe(_paths.Root), "", $"Reload failed: {ex.Message}"),
                    },
                };
                _lastGood = published;
            }
        }

        Changed?.Invoke(this, published);
    }

    private void LogOutcome(DeckConfiguration configuration, bool firstLoad)
    {
        var errors = configuration.Messages.Count(m => m.Severity == ValidationSeverity.Error);
        var warnings = configuration.Messages.Count - errors;

        foreach (var message in configuration.Messages)
        {
            if (message.Severity == ValidationSeverity.Error)
            {
                _logger.Error("Config error: {Message}", message.ToString());
            }
            else
            {
                _logger.Warning("Config warning: {Message}", message.ToString());
            }
        }

        _logger.Information(
            "{Action} config: {Profiles} profiles, {Themes} themes, {Errors} errors, {Warnings} warnings.",
            firstLoad ? "Loaded" : "Reloaded",
            configuration.Profiles.Count,
            configuration.Themes.Count,
            errors,
            warnings);
    }
}
