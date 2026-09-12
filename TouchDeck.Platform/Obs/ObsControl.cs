using OBSWebsocketDotNet;
using Serilog;
using TouchDeck.Core.Abstractions;
using TouchDeck.Core.Configuration;

namespace TouchDeck.Platform.Obs;

/// <summary>
/// Talks to OBS over its websocket. Connecting happens in the background and retries with
/// a widening gap, so OBS starting later, or restarting, sorts itself out without the deck
/// noticing.
/// </summary>
public sealed class ObsControl : IObsControl, IDisposable
{
    private static readonly TimeSpan FirstRetry = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan LongestRetry = TimeSpan.FromMinutes(1);

    private readonly ILogger _logger;
    private readonly OBSWebsocket _client = new();
    private readonly Lock _gate = new();

    private CancellationTokenSource? _reconnecting;
    private ObsConfig _settings = new();
    private TimeSpan _retryIn = FirstRetry;
    private bool _disposed;

    /// <summary>Creates the OBS link.</summary>
    /// <param name="logger">Where connection changes are recorded.</param>
    public ObsControl(ILogger logger)
    {
        _logger = logger.ForContext<ObsControl>();

        _client.Connected += (_, _) =>
        {
            _retryIn = FirstRetry;
            _logger.Information("Connected to OBS.");
        };

        _client.Disconnected += (_, args) =>
        {
            _logger.Information("OBS disconnected: {Reason}", args.DisconnectReason ?? "no reason given");
            ScheduleReconnect();
        };
    }

    /// <inheritdoc />
    public bool IsConnected => _client.IsConnected;

    /// <summary>
    /// Applies settings, connecting, reconnecting or disconnecting as they say. Safe to call
    /// on every config reload.
    /// </summary>
    /// <param name="settings">The OBS block from config.</param>
    public void Configure(ObsConfig settings)
    {
        lock (_gate)
        {
            var wasEnabled = _settings.Enabled;
            var changed = _settings != settings;
            _settings = settings;

            if (!settings.Enabled)
            {
                if (wasEnabled)
                {
                    Stop();
                }

                return;
            }

            if (changed && _client.IsConnected)
            {
                Stop();
            }
        }

        if (settings.Enabled && !_client.IsConnected)
        {
            Connect();
        }
    }

    /// <inheritdoc />
    public Task SendAsync(ObsRequest request, CancellationToken ct)
    {
        if (!_settings.Enabled)
        {
            throw new InvalidOperationException(
                "OBS is switched off in settings, so this button has nothing to talk to.");
        }

        if (!_client.IsConnected)
        {
            throw new InvalidOperationException(
                "OBS is not connected. Check that OBS is running and its websocket server is on.");
        }

        // The client is synchronous, so the call goes to the thread pool to keep the caller free.
        return Task.Run(() => Send(request), ct);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _disposed = true;
        Stop();
    }

    private void Send(ObsRequest request)
    {
        switch (request.Command)
        {
            case ObsCommand.SetScene:
                _client.SetCurrentProgramScene(Required(request.Scene, "scene"));
                break;

            case ObsCommand.ToggleInputMute:
                _client.ToggleInputMute(Required(request.Input, "input"));
                break;

            case ObsCommand.SetInputVolume:
                _client.SetInputVolume(Required(request.Input, "input"), (float)request.Volume, true);
                break;

            case ObsCommand.StartStream:
                _client.StartStream();
                break;

            case ObsCommand.StopStream:
                _client.StopStream();
                break;

            case ObsCommand.ToggleStream:
                _client.ToggleStream();
                break;

            case ObsCommand.StartRecord:
                _client.StartRecord();
                break;

            case ObsCommand.StopRecord:
                _client.StopRecord();
                break;

            case ObsCommand.ToggleRecord:
                _client.ToggleRecord();
                break;

            case ObsCommand.PauseRecord:
                _client.PauseRecord();
                break;

            case ObsCommand.ResumeRecord:
                _client.ResumeRecord();
                break;

            case ObsCommand.SaveReplayBuffer:
                _client.SaveReplayBuffer();
                break;

            case ObsCommand.SetFilterEnabled:
                _client.SetSourceFilterEnabled(
                    Required(request.Source, "source"),
                    Required(request.Filter, "filter"),
                    request.Enabled);
                break;

            case ObsCommand.SetSourceVisible:
                var scene = Required(request.Scene, "scene");
                var source = Required(request.Source, "source");
                var itemId = _client.GetSceneItemId(scene, source, 0);
                _client.SetSceneItemEnabled(scene, itemId, request.Enabled);
                break;
        }
    }

    private static string Required(string? value, string name) =>
        value is { Length: > 0 }
            ? value
            : throw new ArgumentException($"This OBS command needs a \"{name}\" value.");

    private void Connect()
    {
        if (_disposed)
        {
            return;
        }

        var settings = _settings;
        var url = $"ws://{settings.Host}:{settings.Port}";

        try
        {
            _logger.Information("Connecting to OBS at {Url}", url);
            _client.ConnectAsync(url, settings.Password);
        }
        catch (Exception ex)
        {
            _logger.Warning("Could not reach OBS at {Url}: {Reason}", url, ex.Message);
            ScheduleReconnect();
        }
    }

    /// <summary>Waits a while and tries again, waiting longer each time up to a minute.</summary>
    private void ScheduleReconnect()
    {
        if (_disposed || !_settings.Enabled || !_settings.AutoReconnect)
        {
            return;
        }

        CancellationTokenSource source;

        lock (_gate)
        {
            _reconnecting?.Cancel();
            _reconnecting?.Dispose();
            source = _reconnecting = new CancellationTokenSource();
        }

        var wait = _retryIn;
        _retryIn = TimeSpan.FromTicks(Math.Min(LongestRetry.Ticks, _retryIn.Ticks * 2));

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(wait, source.Token).ConfigureAwait(false);

                if (!source.Token.IsCancellationRequested && !_client.IsConnected)
                {
                    Connect();
                }
            }
            catch (OperationCanceledException)
            {
                // Settings changed underneath us, which is fine.
            }
        });
    }

    private void Stop()
    {
        lock (_gate)
        {
            _reconnecting?.Cancel();
            _reconnecting?.Dispose();
            _reconnecting = null;
        }

        try
        {
            if (_client.IsConnected)
            {
                _client.Disconnect();
            }
        }
        catch (Exception ex)
        {
            _logger.Debug(ex, "Disconnecting from OBS threw, which does not matter.");
        }
    }
}
