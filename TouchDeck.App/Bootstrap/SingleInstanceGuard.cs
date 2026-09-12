namespace TouchDeck.App.Bootstrap;

/// <summary>
/// Keeps one deck running at a time, and gives a second launch a way to ask the first one
/// to stop. Until the tray icon exists, this is the only way to close a panel that has no
/// title bar and never takes focus.
/// </summary>
public sealed class SingleInstanceGuard : IDisposable
{
    private const string MutexName = @"Local\TouchDeck.SingleInstance";
    private const string QuitEventName = @"Local\TouchDeck.Quit";
    private const string ConfigureEventName = @"Local\TouchDeck.Configure";

    private readonly Mutex? _mutex;
    private readonly EventWaitHandle _quitRequested;
    private readonly EventWaitHandle _configureRequested;
    private readonly List<RegisteredWaitHandle> _registrations = new();

    private SingleInstanceGuard(
        Mutex? mutex,
        bool isPrimary,
        EventWaitHandle quitRequested,
        EventWaitHandle configureRequested)
    {
        _mutex = mutex;
        IsPrimary = isPrimary;
        _quitRequested = quitRequested;
        _configureRequested = configureRequested;
    }

    /// <summary>True when this process owns the deck.</summary>
    public bool IsPrimary { get; }

    /// <summary>Takes ownership if no other instance holds it.</summary>
    /// <param name="enabled">When false, ownership is assumed without taking the mutex.</param>
    public static SingleInstanceGuard Acquire(bool enabled)
    {
        var quitRequested = new EventWaitHandle(false, EventResetMode.AutoReset, QuitEventName);
        var configureRequested = new EventWaitHandle(false, EventResetMode.AutoReset, ConfigureEventName);

        if (!enabled)
        {
            return new SingleInstanceGuard(null, isPrimary: true, quitRequested, configureRequested);
        }

        var mutex = new Mutex(initiallyOwned: true, MutexName, out var createdNew);
        return new SingleInstanceGuard(mutex, createdNew, quitRequested, configureRequested);
    }

    /// <summary>True when a deck is already running on this machine.</summary>
    public static bool IsDeckRunning()
    {
        if (!Mutex.TryOpenExisting(MutexName, out var existing))
        {
            return false;
        }

        existing.Dispose();
        return true;
    }

    /// <summary>Asks a running instance to open the config center.</summary>
    public static void RequestConfigure()
    {
        using var configureRequested = new EventWaitHandle(false, EventResetMode.AutoReset, ConfigureEventName);
        configureRequested.Set();
    }

    /// <summary>Signals any running instance to shut down.</summary>
    public static void RequestQuit()
    {
        using var quitRequested = new EventWaitHandle(false, EventResetMode.AutoReset, QuitEventName);
        quitRequested.Set();
    }

    /// <summary>Runs <paramref name="onQuitRequested"/> when another launch asks this instance to stop.</summary>
    /// <param name="onQuitRequested">What to do, usually shutting the application down.</param>
    public void ListenForQuitRequest(Action onQuitRequested) =>
        _registrations.Add(ThreadPool.RegisterWaitForSingleObject(
            _quitRequested,
            (_, _) => onQuitRequested(),
            null,
            System.Threading.Timeout.Infinite,
            executeOnlyOnce: true));

    /// <summary>Runs <paramref name="onConfigureRequested"/> whenever a launch asks for the config center.</summary>
    /// <param name="onConfigureRequested">What to do, usually showing the config window.</param>
    public void ListenForConfigureRequest(Action onConfigureRequested) =>
        _registrations.Add(ThreadPool.RegisterWaitForSingleObject(
            _configureRequested,
            (_, _) => onConfigureRequested(),
            null,
            System.Threading.Timeout.Infinite,
            executeOnlyOnce: false));

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var registration in _registrations)
        {
            registration.Unregister(null);
        }

        _registrations.Clear();

        if (_mutex is not null && IsPrimary)
        {
            _mutex.ReleaseMutex();
        }

        _mutex?.Dispose();
        _quitRequested.Dispose();
        _configureRequested.Dispose();
    }
}
