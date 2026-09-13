using TouchDeck.Core.Configuration;
using TouchDeck.Platform.Display;

namespace TouchDeck.App.Configurator;

/// <summary>An attached monitor, offered in the display dropdown.</summary>
/// <param name="Monitor">The monitor itself, or null for the "type it yourself" entry.</param>
public sealed record MonitorChoice(MonitorInfo? Monitor)
{
    /// <summary>How the monitor reads in the dropdown.</summary>
    public string DisplayName => Monitor is null
        ? "Other, type the name yourself"
        : $"{Monitor.DeviceName}  {Monitor.Width}x{Monitor.Height}" +
          $"{(Monitor.IsPrimary ? "  primary" : string.Empty)}  at {Monitor.DpiX * 100 / 96}%";
}

/// <summary>Editable global settings, which is everything in config.json.</summary>
public sealed class SettingsEditModel : ObservableObject
{
    private readonly AppConfig _original;

    private DisplaySelect _select;
    private string? _value;
    private int? _fallbackToIndex;
    private bool _fullscreen;
    private string _defaultProfile;
    private bool _hotReload;
    private bool _singleInstance;
    private bool _preventDisplaySleep;
    private bool _startWithWindows;
    private bool _startMinimisedToTray;
    private bool _wakeOnTouch;
    private bool _claimTouchInput;
    private double _dimAfterSeconds;
    private double _dimOpacity;
    private int _longPressMs;
    private int _doubleTapMs;
    private int _actionTimeoutMs;
    private LogLevel _logLevel;
    private int _retainDays;
    private MonitorChoice? _selectedMonitor;

    /// <summary>Creates an editable copy of the global settings.</summary>
    /// <param name="config">The settings as configured.</param>
    /// <param name="monitors">The monitors currently attached.</param>
    public SettingsEditModel(AppConfig config, IReadOnlyList<MonitorInfo> monitors)
    {
        _original = config;

        _select = config.Display.Select;
        _value = config.Display.Value;
        _fallbackToIndex = config.Display.FallbackToIndex;
        _fullscreen = config.Display.Fullscreen;

        _defaultProfile = config.Behaviour.DefaultProfile;
        _hotReload = config.Behaviour.HotReload;
        _singleInstance = config.Behaviour.SingleInstance;
        _preventDisplaySleep = config.Behaviour.PreventDisplaySleep;
        _startWithWindows = config.Behaviour.StartWithWindows;
        _startMinimisedToTray = config.Behaviour.StartMinimisedToTray;
        _wakeOnTouch = config.Behaviour.WakeOnTouch;
        _claimTouchInput = config.Behaviour.ClaimTouchInput;
        _dimAfterSeconds = config.Behaviour.DimAfterSeconds;
        _dimOpacity = config.Behaviour.DimOpacity;
        _longPressMs = config.Behaviour.LongPressMs;
        _doubleTapMs = config.Behaviour.DoubleTapMs;
        _actionTimeoutMs = config.Behaviour.ActionTimeoutMs;

        _logLevel = config.Logging.Level;
        _retainDays = config.Logging.RetainDays;

        Monitors = monitors
            .Select(m => new MonitorChoice(m))
            .Append(new MonitorChoice(null))
            .ToArray();

        _selectedMonitor = Monitors.FirstOrDefault(m =>
            m.Monitor is not null && MatchesCurrentSelection(m.Monitor)) ?? Monitors[^1];
    }

    /// <summary>The monitors that can be chosen right now.</summary>
    public IReadOnlyList<MonitorChoice> Monitors { get; }

    /// <summary>Every way of picking a monitor.</summary>
    public static IReadOnlyList<DisplaySelect> SelectModes { get; } = Enum.GetValues<DisplaySelect>();

    /// <summary>Every logging level.</summary>
    public static IReadOnlyList<LogLevel> LogLevels { get; } = Enum.GetValues<LogLevel>();

    /// <summary>
    /// Picking a monitor here rewrites the selector to match by device name, which is the
    /// choice that survives replugging.
    /// </summary>
    public MonitorChoice? SelectedMonitor
    {
        get => _selectedMonitor;
        set
        {
            if (!Set(ref _selectedMonitor, value) || value?.Monitor is not { } monitor)
            {
                return;
            }

            Select = DisplaySelect.ByName;
            Value = monitor.DeviceName;
        }
    }

    /// <summary>How the monitor is chosen.</summary>
    public DisplaySelect Select
    {
        get => _select;
        set => Set(ref _select, value);
    }

    /// <summary>The device name, index or resolution the selector matches on.</summary>
    public string? Value
    {
        get => _value;
        set => Set(ref _value, value);
    }

    /// <summary>Monitor used when the selector matches nothing.</summary>
    public int? FallbackToIndex
    {
        get => _fallbackToIndex;
        set => Set(ref _fallbackToIndex, value);
    }

    /// <summary>Cover the whole monitor.</summary>
    public bool Fullscreen
    {
        get => _fullscreen;
        set => Set(ref _fullscreen, value);
    }

    /// <summary>The profile shown at startup.</summary>
    public string DefaultProfile
    {
        get => _defaultProfile;
        set => Set(ref _defaultProfile, value);
    }

    /// <summary>Reload the config folder whenever a file in it is saved.</summary>
    public bool HotReload
    {
        get => _hotReload;
        set => Set(ref _hotReload, value);
    }

    /// <summary>Refuse to start a second deck.</summary>
    public bool SingleInstance
    {
        get => _singleInstance;
        set => Set(ref _singleInstance, value);
    }

    /// <summary>Keep the touchscreen awake.</summary>
    public bool PreventDisplaySleep
    {
        get => _preventDisplaySleep;
        set => Set(ref _preventDisplaySleep, value);
    }

    /// <summary>Start the deck when Windows starts.</summary>
    public bool StartWithWindows
    {
        get => _startWithWindows;
        set => Set(ref _startWithWindows, value);
    }

    /// <summary>Start hidden in the tray rather than showing the panel.</summary>
    public bool StartMinimisedToTray
    {
        get => _startMinimisedToTray;
        set => Set(ref _startMinimisedToTray, value);
    }

    /// <summary>Wake a dimmed panel on touch, without firing the button.</summary>
    public bool WakeOnTouch
    {
        get => _wakeOnTouch;
        set => Set(ref _wakeOnTouch, value);
    }

    /// <summary>Take touch from Windows directly, so a tap does not move the mouse pointer.</summary>
    public bool ClaimTouchInput
    {
        get => _claimTouchInput;
        set => Set(ref _claimTouchInput, value);
    }

    /// <summary>Seconds of no touches before the panel dims. Zero turns dimming off.</summary>
    public double DimAfterSeconds
    {
        get => _dimAfterSeconds;
        set => Set(ref _dimAfterSeconds, Math.Max(0, value));
    }

    /// <summary>How dim the panel goes, where 1 is not dim at all.</summary>
    public double DimOpacity
    {
        get => _dimOpacity;
        set => Set(ref _dimOpacity, Math.Clamp(value, 0, 1));
    }

    /// <summary>How long a press must be held to count as a long press.</summary>
    public int LongPressMs
    {
        get => _longPressMs;
        set => Set(ref _longPressMs, Math.Max(0, value));
    }

    /// <summary>How quickly a second press must come to count as a double tap.</summary>
    public int DoubleTapMs
    {
        get => _doubleTapMs;
        set => Set(ref _doubleTapMs, Math.Max(0, value));
    }

    /// <summary>How long an action may run before it is cancelled.</summary>
    public int ActionTimeoutMs
    {
        get => _actionTimeoutMs;
        set => Set(ref _actionTimeoutMs, Math.Max(1, value));
    }

    /// <summary>How much detail goes into the log.</summary>
    public LogLevel LogLevel
    {
        get => _logLevel;
        set => Set(ref _logLevel, value);
    }

    /// <summary>How many days of log files to keep. Takes effect at the next start.</summary>
    public int RetainDays
    {
        get => _retainDays;
        set => Set(ref _retainDays, Math.Max(1, value));
    }

    /// <summary>Builds the config record, keeping the parts the editor does not cover.</summary>
    public AppConfig ToConfig() => _original with
    {
        Display = _original.Display with
        {
            Select = _select,
            Value = string.IsNullOrWhiteSpace(_value) ? null : _value,
            FallbackToIndex = _fallbackToIndex,
            Fullscreen = _fullscreen,
        },
        Behaviour = _original.Behaviour with
        {
            DefaultProfile = _defaultProfile,
            HotReload = _hotReload,
            SingleInstance = _singleInstance,
            PreventDisplaySleep = _preventDisplaySleep,
            StartWithWindows = _startWithWindows,
            StartMinimisedToTray = _startMinimisedToTray,
            WakeOnTouch = _wakeOnTouch,
            ClaimTouchInput = _claimTouchInput,
            DimAfterSeconds = _dimAfterSeconds,
            DimOpacity = _dimOpacity,
            LongPressMs = _longPressMs,
            DoubleTapMs = _doubleTapMs,
            ActionTimeoutMs = _actionTimeoutMs,
        },
        Logging = _original.Logging with
        {
            Level = _logLevel,
            RetainDays = _retainDays,
        },
    };

    private bool MatchesCurrentSelection(MonitorInfo monitor) => _select switch
    {
        DisplaySelect.ByName => string.Equals(monitor.DeviceName, _value, StringComparison.OrdinalIgnoreCase),
        DisplaySelect.ByResolution => string.Equals(monitor.Resolution, _value, StringComparison.OrdinalIgnoreCase),
        DisplaySelect.ByIndex => int.TryParse(_value, out var index) && index == monitor.Index,
        DisplaySelect.Primary => monitor.IsPrimary,
        _ => false,
    };
}
