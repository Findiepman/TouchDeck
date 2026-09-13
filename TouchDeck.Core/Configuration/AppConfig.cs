using System.Text.Json.Serialization;

namespace TouchDeck.Core.Configuration;

/// <summary>
/// Root model for <c>config.json</c>. Every member has a default so that an empty
/// object, or a missing file, still produces a runnable configuration.
/// </summary>
public sealed record AppConfig
{
    /// <summary>Points editors at the generated schema. Kept so saving does not drop it.</summary>
    [JsonPropertyName("$schema")]
    public string? Schema { get; init; } = "./touchdeck.schema.json";

    /// <summary>Config schema version. Bumped when a breaking change lands.</summary>
    public int Version { get; init; } = 1;

    public DisplayConfig Display { get; init; } = new();

    public BehaviourConfig Behaviour { get; init; } = new();

    public FeedbackConfig Feedback { get; init; } = new();

    public IntegrationsConfig Integrations { get; init; } = new();

    public LoggingConfig Logging { get; init; } = new();
}

/// <summary>How the target monitor is chosen.</summary>
public enum DisplaySelect
{
    /// <summary>Match the stable Windows device name, e.g. <c>\.\DISPLAY2</c>.</summary>
    ByName,

    /// <summary>Match by index into the enumerated monitor list.</summary>
    ByIndex,

    /// <summary>Match by resolution, e.g. <c>1024x600</c>.</summary>
    ByResolution,

    /// <summary>Use whichever monitor Windows reports as primary.</summary>
    Primary,
}

/// <summary>Which monitor the deck occupies and how much of it.</summary>
public sealed record DisplayConfig
{
    public DisplaySelect Select { get; init; } = DisplaySelect.Primary;

    /// <summary>
    /// Selector payload. A device name for <see cref="DisplaySelect.ByName"/>, an integer
    /// for <see cref="DisplaySelect.ByIndex"/>, a <c>WIDTHxHEIGHT</c> string for
    /// <see cref="DisplaySelect.ByResolution"/>, ignored for <see cref="DisplaySelect.Primary"/>.
    /// </summary>
    public string? Value { get; init; }

    /// <summary>Monitor index used when <see cref="Value"/> matches nothing.</summary>
    public int? FallbackToIndex { get; init; }

    /// <summary>Cover the whole monitor. When false, <see cref="Bounds"/> is used.</summary>
    public bool Fullscreen { get; init; } = true;

    /// <summary>Explicit window rectangle, in device independent units, relative to the monitor.</summary>
    public BoundsConfig? Bounds { get; init; }
}

/// <summary>A rectangle in device independent units, relative to the target monitor's top left.</summary>
public sealed record BoundsConfig
{
    public double X { get; init; }

    public double Y { get; init; }

    public double Width { get; init; } = 800;

    public double Height { get; init; } = 480;
}

/// <summary>Runtime behaviour that is not visual.</summary>
public sealed record BehaviourConfig
{
    public bool StartWithWindows { get; init; }

    public bool StartMinimisedToTray { get; init; }

    public bool PreventDisplaySleep { get; init; } = true;

    public bool SingleInstance { get; init; } = true;

    public string DefaultProfile { get; init; } = "default";

    /// <summary>Seconds of inactivity before the panel dims. Zero disables dimming.</summary>
    public double DimAfterSeconds { get; init; } = 300;

    public double DimOpacity { get; init; } = 0.25;

    public bool WakeOnTouch { get; init; } = true;

    /// <summary>
    /// Take touch straight from Windows rather than letting it be turned into mouse clicks.
    /// A window that has not asked for touch gets the pointer moved to wherever the finger
    /// landed, which on a second screen means every tap drags the pointer off the screen you
    /// are looking at. A game that steers by mouse movement reads that as a huge flick.
    /// Turn it off only if touch stops working.
    /// </summary>
    public bool ClaimTouchInput { get; init; } = true;

    public int LongPressMs { get; init; } = 500;

    public int DoubleTapMs { get; init; } = 250;

    public bool HotReload { get; init; } = true;

    /// <summary>Milliseconds an action may run before it is cancelled.</summary>
    public int ActionTimeoutMs { get; init; } = 10_000;
}

/// <summary>Non-visual press feedback.</summary>
public sealed record FeedbackConfig
{
    /// <summary>Path to a wav file played on press, or null for silence.</summary>
    public string? PressSound { get; init; }

    public double PressSoundVolume { get; init; } = 0.5;
}

/// <summary>External services the deck talks to.</summary>
public sealed record IntegrationsConfig
{
    public ObsConfig Obs { get; init; } = new();
}

/// <summary>OBS websocket connection settings.</summary>
public sealed record ObsConfig
{
    public bool Enabled { get; init; }

    public string Host { get; init; } = "localhost";

    public int Port { get; init; } = 4455;

    public string Password { get; init; } = "";

    public bool AutoReconnect { get; init; } = true;
}

/// <summary>File logging settings.</summary>
public sealed record LoggingConfig
{
    public LogLevel Level { get; init; } = LogLevel.Information;

    public int RetainDays { get; init; } = 7;
}

/// <summary>Mirrors Serilog's level set without leaking the dependency into config.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<LogLevel>))]
public enum LogLevel
{
    Verbose,
    Debug,
    Information,
    Warning,
    Error,
    Fatal,
}
