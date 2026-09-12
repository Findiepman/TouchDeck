using TouchDeck.Core.Abstractions;
using TouchDeck.Core.Input;

namespace TouchDeck.Tests;

/// <summary>An injector that writes down what it was asked to send instead of sending it.</summary>
public sealed class RecordingInjector : IInputInjector
{
    /// <summary>Combinations sent with a press and release.</summary>
    public List<KeyCombo> Sent { get; } = new();

    /// <summary>Combinations left held down.</summary>
    public List<KeyCombo> Held { get; } = new();

    /// <summary>Combinations released.</summary>
    public List<KeyCombo> Released { get; } = new();

    /// <summary>Text typed out.</summary>
    public List<string> Typed { get; } = new();

    /// <summary>Mouse buttons pressed, as a button and what was done to it.</summary>
    public List<(MouseButton Button, PressAction Action)> Clicks { get; } = new();

    /// <summary>Pointer moves, as a position and whether it was relative.</summary>
    public List<(int X, int Y, bool Relative)> Moves { get; } = new();

    /// <summary>Scrolls, as a number of notches and a direction.</summary>
    public List<(int Amount, ScrollDirection Direction)> Scrolls { get; } = new();

    /// <inheritdoc />
    public void SendCombo(KeyCombo combo) => Sent.Add(combo);

    /// <inheritdoc />
    public void HoldCombo(KeyCombo combo) => Held.Add(combo);

    /// <inheritdoc />
    public void ReleaseCombo(KeyCombo combo) => Released.Add(combo);

    /// <inheritdoc />
    public void ReleaseAllHeldKeys() => Held.Clear();

    /// <inheritdoc />
    public void TypeText(string text) => Typed.Add(text);

    /// <inheritdoc />
    public void MouseButton(MouseButton button, PressAction action) => Clicks.Add((button, action));

    /// <inheritdoc />
    public void MoveMouse(int x, int y, bool relative) => Moves.Add((x, y, relative));

    /// <inheritdoc />
    public void Scroll(int amount, ScrollDirection direction) => Scrolls.Add((amount, direction));
}

/// <summary>A launcher that records requests rather than starting anything.</summary>
public sealed class RecordingLauncher : IProcessLauncher
{
    /// <summary>What it was asked to start.</summary>
    public List<LaunchRequest> Launched { get; } = new();

    /// <inheritdoc />
    public void Launch(LaunchRequest request) => Launched.Add(request);
}

/// <summary>A clipboard held in a field.</summary>
public sealed class FakeClipboard : IClipboard
{
    private string? _text;

    /// <inheritdoc />
    public string? GetText() => _text;

    /// <inheritdoc />
    public void SetText(string text) => _text = text;

    /// <inheritdoc />
    public void Clear() => _text = null;
}

/// <summary>A window manager that says yes to anything it has been told exists.</summary>
public sealed class FakeWindows : IWindowManager
{
    /// <summary>Window descriptions this manager pretends to find.</summary>
    public HashSet<string> Existing { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>What it was asked to do.</summary>
    public List<(WindowOperation Operation, WindowMatch Match)> Applied { get; } = new();

    /// <inheritdoc />
    public string? ForegroundProcessName { get; set; }

    /// <inheritdoc />
    public bool Apply(WindowOperation operation, WindowMatch match)
    {
        Applied.Add((operation, match));
        return Existing.Contains(match.ProcessName ?? match.TitleRegex ?? "");
    }
}

/// <summary>A mixer that remembers levels instead of touching the sound card.</summary>
public sealed class FakeMixer : IAudioMixer
{
    /// <summary>What it was asked to change.</summary>
    public List<(AudioTarget Target, string? Name, AudioOperation Operation, double Value)> Applied { get; } = new();

    /// <summary>The level it reports.</summary>
    public double Volume { get; set; } = 0.5;

    /// <summary>The mute state it reports.</summary>
    public bool Muted { get; set; }

    /// <inheritdoc />
    public void Apply(AudioTarget target, string? name, AudioOperation operation, double value)
    {
        Applied.Add((target, name, operation, value));

        switch (operation)
        {
            case AudioOperation.Mute:
                Muted = true;
                break;
            case AudioOperation.Unmute:
                Muted = false;
                break;
            case AudioOperation.ToggleMute:
                Muted = !Muted;
                break;
            case AudioOperation.Set:
                Volume = value;
                break;
            case AudioOperation.Adjust:
                Volume = Math.Clamp(Volume + value, 0, 1);
                break;
        }
    }

    /// <inheritdoc />
    public double GetVolume(AudioTarget target, string? name) => Volume;

    /// <inheritdoc />
    public bool IsMuted(AudioTarget target, string? name) => Muted;
}

/// <summary>A shell that returns a canned answer.</summary>
public sealed class FakeShell : IShellRunner
{
    /// <summary>What it was asked to run.</summary>
    public List<(string Command, ShellKind Shell, bool Hidden, bool Captured)> Ran { get; } = new();

    /// <summary>What it pretends the command printed.</summary>
    public string Output { get; set; } = "";

    /// <inheritdoc />
    public Task<string> RunAsync(string command, ShellKind shell, bool hidden, bool captureOutput, CancellationToken ct)
    {
        Ran.Add((command, shell, hidden, captureOutput));
        return Task.FromResult(Output);
    }
}

/// <summary>An HTTP sender that answers without a network.</summary>
public sealed class FakeHttp : IHttpSender
{
    /// <summary>What it was asked to send.</summary>
    public List<(string Method, string Url, string? Body)> Sent { get; } = new();

    /// <summary>What it pretends the server answered.</summary>
    public string Response { get; set; } = "";

    /// <inheritdoc />
    public Task<string> SendAsync(
        string method,
        string url,
        IReadOnlyDictionary<string, string> headers,
        string? body,
        CancellationToken ct)
    {
        Sent.Add((method, url, body));
        return Task.FromResult(Response);
    }
}

/// <summary>A script runner that records snippets and can pretend AutoHotkey is missing.</summary>
public sealed class FakeScripts : IScriptRunner
{
    /// <summary>Snippets it was asked to run.</summary>
    public List<string> Ran { get; } = new();

    /// <inheritdoc />
    public bool IsAutoHotkeyInstalled { get; set; } = true;

    /// <inheritdoc />
    public Task RunAutoHotkeyAsync(string script, CancellationToken ct)
    {
        Ran.Add(script);
        return Task.CompletedTask;
    }
}

/// <summary>An OBS link that records commands and can pretend OBS is not there.</summary>
public sealed class FakeObs : IObsControl
{
    /// <summary>Commands it was asked to send.</summary>
    public List<ObsRequest> Sent { get; } = new();

    /// <inheritdoc />
    public bool IsConnected { get; set; } = true;

    /// <inheritdoc />
    public Task SendAsync(ObsRequest request, CancellationToken ct)
    {
        if (!IsConnected)
        {
            throw new InvalidOperationException("OBS is not connected.");
        }

        Sent.Add(request);
        return Task.CompletedTask;
    }
}

/// <summary>A deck controller that writes down where it was told to go.</summary>
public sealed class FakeDeck : IDeckController
{
    /// <summary>Every move it was asked to make, in order.</summary>
    public List<string> Moves { get; } = new();

    /// <inheritdoc />
    public string? CurrentProfileId { get; set; } = "default";

    /// <inheritdoc />
    public string? CurrentPageId { get; set; } = "main";

    /// <inheritdoc />
    public void SwitchProfile(string profileId) => Moves.Add($"profile:{profileId}");

    /// <inheritdoc />
    public void SwitchPage(string pageId) => Moves.Add($"page:{pageId}");

    /// <inheritdoc />
    public void NextPage() => Moves.Add("next");

    /// <inheritdoc />
    public void PreviousPage() => Moves.Add("previous");

    /// <inheritdoc />
    public void OpenFolder(string pageId) => Moves.Add($"open:{pageId}");

    /// <inheritdoc />
    public void CloseFolder() => Moves.Add("close");
}
