namespace TouchDeck.Core.Abstractions;

/// <summary>Which mouse button an action means.</summary>
public enum MouseButton
{
    Left,
    Right,
    Middle,
}

/// <summary>What to do with a button, whether a mouse button or a key.</summary>
public enum PressAction
{
    /// <summary>Press and release.</summary>
    Click,

    /// <summary>Press and hold.</summary>
    Down,

    /// <summary>Release something being held.</summary>
    Up,
}

/// <summary>Which way a scroll goes.</summary>
public enum ScrollDirection
{
    Up,
    Down,
    Left,
    Right,
}

/// <summary>What to do with a window.</summary>
public enum WindowOperation
{
    Focus,
    Minimise,
    Maximise,
    Restore,
    Close,
}

/// <summary>How to find a window.</summary>
/// <param name="ProcessName">Executable name, with or without the extension.</param>
/// <param name="TitleRegex">Regular expression matched against the window title.</param>
public sealed record WindowMatch(string? ProcessName, string? TitleRegex);

/// <summary>Finds and commands other applications' windows.</summary>
public interface IWindowManager
{
    /// <summary>The executable name of whatever is in front, or null when nothing is.</summary>
    string? ForegroundProcessName { get; }

    /// <summary>Applies an operation to the first window that matches.</summary>
    /// <param name="operation">What to do.</param>
    /// <param name="match">How to find the window.</param>
    /// <returns>False when no window matched.</returns>
    bool Apply(WindowOperation operation, WindowMatch match);
}

/// <summary>Reads and writes the Windows clipboard.</summary>
public interface IClipboard
{
    /// <summary>The clipboard's text, or null when it holds something else.</summary>
    string? GetText();

    /// <summary>Puts text on the clipboard.</summary>
    /// <param name="text">What to put there.</param>
    void SetText(string text);

    /// <summary>Empties the clipboard.</summary>
    void Clear();
}

/// <summary>Whose volume an audio action means.</summary>
public enum AudioTarget
{
    /// <summary>The default playback device.</summary>
    Default,

    /// <summary>A device named in the action.</summary>
    Device,

    /// <summary>One application's own volume slider.</summary>
    Process,
}

/// <summary>What to do to a volume.</summary>
public enum AudioOperation
{
    Mute,
    Unmute,
    ToggleMute,

    /// <summary>Set the level, 0 to 1.</summary>
    Set,

    /// <summary>Move the level by an amount, which may be negative.</summary>
    Adjust,
}

/// <summary>Controls playback volume, per device or per application.</summary>
public interface IAudioMixer
{
    /// <summary>Changes a volume or mute state.</summary>
    /// <param name="target">Whose volume.</param>
    /// <param name="name">Device or process name, when the target needs one.</param>
    /// <param name="operation">What to do.</param>
    /// <param name="value">Level for set, or delta for adjust, on a 0 to 1 scale.</param>
    void Apply(AudioTarget target, string? name, AudioOperation operation, double value);

    /// <summary>Reads a volume on a 0 to 1 scale.</summary>
    /// <param name="target">Whose volume.</param>
    /// <param name="name">Device or process name, when the target needs one.</param>
    double GetVolume(AudioTarget target, string? name);

    /// <summary>Reads a mute state.</summary>
    /// <param name="target">Whose volume.</param>
    /// <param name="name">Device or process name, when the target needs one.</param>
    bool IsMuted(AudioTarget target, string? name);
}

/// <summary>Which shell runs a command.</summary>
public enum ShellKind
{
    PowerShell,
    Cmd,
}

/// <summary>Runs shell commands.</summary>
public interface IShellRunner
{
    /// <summary>Runs a command and optionally waits for what it printed.</summary>
    /// <param name="command">The command line to run.</param>
    /// <param name="shell">Which shell to run it in.</param>
    /// <param name="hidden">Whether to hide the console window.</param>
    /// <param name="captureOutput">Whether to wait for the command and return its output.</param>
    /// <param name="ct">Cancels the command.</param>
    /// <returns>What the command printed, or an empty string when output was not captured.</returns>
    Task<string> RunAsync(string command, ShellKind shell, bool hidden, bool captureOutput, CancellationToken ct);
}

/// <summary>Sends a web request, for talking to anything with an HTTP API.</summary>
public interface IHttpSender
{
    /// <summary>Sends a request and returns the response body.</summary>
    /// <param name="method">GET, POST and so on.</param>
    /// <param name="url">Where to send it.</param>
    /// <param name="headers">Extra request headers.</param>
    /// <param name="body">The request body, or null.</param>
    /// <param name="ct">Cancels the request.</param>
    Task<string> SendAsync(
        string method,
        string url,
        IReadOnlyDictionary<string, string> headers,
        string? body,
        CancellationToken ct);
}

/// <summary>Runs a script in another tool, so the deck can do things it has no action for.</summary>
public interface IScriptRunner
{
    /// <summary>Whether AutoHotkey could be found on this machine.</summary>
    bool IsAutoHotkeyInstalled { get; }

    /// <summary>Runs an AutoHotkey v2 snippet.</summary>
    /// <param name="script">The script text.</param>
    /// <param name="ct">Cancels the script.</param>
    Task RunAutoHotkeyAsync(string script, CancellationToken ct);
}

/// <summary>What an OBS action asks OBS to do.</summary>
public enum ObsCommand
{
    SetScene,
    ToggleInputMute,
    SetInputVolume,
    StartStream,
    StopStream,
    ToggleStream,
    StartRecord,
    StopRecord,
    ToggleRecord,
    PauseRecord,
    ResumeRecord,
    SaveReplayBuffer,
    SetFilterEnabled,
    SetSourceVisible,
}

/// <summary>Everything one OBS action might carry.</summary>
/// <param name="Command">What to do.</param>
/// <param name="Scene">Scene name, for scene and source commands.</param>
/// <param name="Input">Input name, for mute and volume commands.</param>
/// <param name="Source">Source name, for filter and visibility commands.</param>
/// <param name="Filter">Filter name.</param>
/// <param name="Enabled">Whether the filter or source ends up on.</param>
/// <param name="Volume">Volume in decibels, for the volume command.</param>
public sealed record ObsRequest(
    ObsCommand Command,
    string? Scene = null,
    string? Input = null,
    string? Source = null,
    string? Filter = null,
    bool Enabled = true,
    double Volume = 0);

/// <summary>Talks to OBS over its websocket.</summary>
public interface IObsControl
{
    /// <summary>Whether OBS is connected right now.</summary>
    bool IsConnected { get; }

    /// <summary>Sends one command to OBS.</summary>
    /// <param name="request">What to do.</param>
    /// <param name="ct">Cancels waiting for OBS.</param>
    Task SendAsync(ObsRequest request, CancellationToken ct);
}
