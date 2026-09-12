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

    private readonly Mutex? _mutex;
    private readonly EventWaitHandle _quitRequested;
    private RegisteredWaitHandle? _registration;

    private SingleInstanceGuard(Mutex? mutex, bool isPrimary, EventWaitHandle quitRequested)
    {
        _mutex = mutex;
        IsPrimary = isPrimary;
        _quitRequested = quitRequested;
    }

    /// <summary>True when this process owns the deck.</summary>
    public bool IsPrimary { get; }

    /// <summary>Takes ownership if no other instance holds it.</summary>
    /// <param name="enabled">When false, ownership is assumed without taking the mutex.</param>
    public static SingleInstanceGuard Acquire(bool enabled)
    {
        var quitRequested = new EventWaitHandle(false, EventResetMode.AutoReset, QuitEventName);

        if (!enabled)
        {
            return new SingleInstanceGuard(null, isPrimary: true, quitRequested);
        }

        var mutex = new Mutex(initiallyOwned: true, MutexName, out var createdNew);
        return new SingleInstanceGuard(mutex, createdNew, quitRequested);
    }

    /// <summary>Signals any running instance to shut down.</summary>
    public static void RequestQuit()
    {
        using var quitRequested = new EventWaitHandle(false, EventResetMode.AutoReset, QuitEventName);
        quitRequested.Set();
    }

    /// <summary>Runs <paramref name="onQuitRequested"/> when another launch asks this instance to stop.</summary>
    /// <param name="onQuitRequested">What to do, usually shutting the application down.</param>
    public void ListenForQuitRequest(Action onQuitRequested)
    {
        _registration = ThreadPool.RegisterWaitForSingleObject(
            _quitRequested,
            (_, _) => onQuitRequested(),
            null,
            System.Threading.Timeout.Infinite,
            executeOnlyOnce: true);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _registration?.Unregister(null);
        _registration = null;

        if (_mutex is not null && IsPrimary)
        {
            _mutex.ReleaseMutex();
        }

        _mutex?.Dispose();
        _quitRequested.Dispose();
    }
}
