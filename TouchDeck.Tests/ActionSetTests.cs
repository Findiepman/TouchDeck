using System.Text.Json;
using Serilog.Core;
using TouchDeck.Actions;
using TouchDeck.Core.Abstractions;
using TouchDeck.Core.Actions;
using TouchDeck.Core.Configuration;
using TouchDeck.Core.Expressions;
using TouchDeck.Core.Input;
using TouchDeck.Core.Variables;
using Xunit;

namespace TouchDeck.Tests;

/// <summary>
/// Every action type, driven through the real dispatcher with fake platform services. This
/// is where "the button does what the JSON says" is actually pinned down.
/// </summary>
public sealed class ActionSetTests
{
    private readonly RecordingInjector _input = new();
    private readonly RecordingLauncher _launcher = new();
    private readonly FakeClipboard _clipboard = new();
    private readonly FakeWindows _windows = new();
    private readonly FakeMixer _audio = new();
    private readonly FakeShell _shell = new();
    private readonly FakeHttp _http = new();
    private readonly FakeScripts _scripts = new();
    private readonly FakeObs _obs = new();
    private readonly FakeDeck _deck = new();
    private readonly VariableStore _variables = new();
    private readonly ActionDispatcher _dispatcher;

    private string? _lastFailure;

    public ActionSetTests()
    {
        var services = new ServiceRegistry()
            .Add<IInputInjector>(_input)
            .Add<IProcessLauncher>(_launcher)
            .Add<IClipboard>(_clipboard)
            .Add<IWindowManager>(_windows)
            .Add<IAudioMixer>(_audio)
            .Add<IShellRunner>(_shell)
            .Add<IHttpSender>(_http)
            .Add<IScriptRunner>(_scripts)
            .Add<IObsControl>(_obs)
            .Add<IDeckController>(_deck)
            .Add<IVariableStore>(_variables)
            .Add<IValueResolver>(new DeckValueResolver(_variables));

        _dispatcher = new ActionDispatcher(
            ActionRegistry.Scan(Logger.None, typeof(HotkeyAction).Assembly),
            services,
            Logger.None);

        _dispatcher.Failed += (_, failure) => _lastFailure = failure.Message;
    }

    private Task<bool> Run(string json) =>
        _dispatcher.ExecuteAsync(JsonSerializer.Deserialize<ActionConfig>(json, ConfigJson.Options)!, default);

    private async Task<string> Fails(string json)
    {
        _lastFailure = null;
        Assert.False(await Run(json));
        Assert.NotNull(_lastFailure);
        return _lastFailure!;
    }

    // ------------------------------------------------------------------ input

    [Fact]
    public async Task KeyDownHoldsAndKeyUpReleases()
    {
        Assert.True(await Run("""{ "type": "keyDown", "keys": "f13" }"""));
        Assert.True(await Run("""{ "type": "keyUp", "keys": "f13" }"""));

        Assert.Equal(VirtualKey.F13, Assert.Single(_input.Held).Key);
        Assert.Equal(VirtualKey.F13, Assert.Single(_input.Released).Key);
    }

    [Fact]
    public async Task TextIsTypedVerbatim()
    {
        Assert.True(await Run("""{ "type": "text", "value": "Thanks for the follow!" }"""));

        Assert.Equal("Thanks for the follow!", Assert.Single(_input.Typed));
    }

    [Fact]
    public async Task AMouseClickCanMoveFirst()
    {
        Assert.True(await Run("""{ "type": "mouse", "button": "right", "action": "click", "x": 400, "y": 300 }"""));

        Assert.Equal((400, 300, false), Assert.Single(_input.Moves));
        Assert.Equal((MouseButton.Right, PressAction.Click), Assert.Single(_input.Clicks));
    }

    [Fact]
    public async Task AMouseClickWithNoPositionStaysPut()
    {
        Assert.True(await Run("""{ "type": "mouse" }"""));

        Assert.Empty(_input.Moves);
        Assert.Equal((MouseButton.Left, PressAction.Click), Assert.Single(_input.Clicks));
    }

    [Fact]
    public async Task ScrollingTakesADirectionAndAnAmount()
    {
        Assert.True(await Run("""{ "type": "scroll", "direction": "up", "amount": 3 }"""));

        Assert.Equal((3, ScrollDirection.Up), Assert.Single(_input.Scrolls));
    }

    [Fact]
    public async Task AnUnknownChoiceListsTheRealOnes()
    {
        var message = await Fails("""{ "type": "scroll", "direction": "sideways" }""");

        Assert.Contains("sideways", message, StringComparison.Ordinal);
        Assert.Contains("up", message, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------ apps and system

    [Fact]
    public async Task LaunchCarriesItsFocusOptions()
    {
        Assert.True(await Run("""{ "type": "launch", "path": "obs64.exe", "focusIfRunning": true }"""));

        var request = Assert.Single(_launcher.Launched);
        Assert.Equal("obs64.exe", request.Path);
        Assert.True(request.FocusIfRunning);
    }

    [Fact]
    public async Task OpenRefusesSomethingThatIsNotAnAddress()
    {
        var message = await Fails("""{ "type": "open", "url": "not a url" }""");

        Assert.Contains("not a web address", message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShellKeepsWhatTheCommandPrinted()
    {
        _shell.Output = "14:05";

        Assert.True(await Run("""{ "type": "shell", "command": "Get-Date", "captureOutputTo": "clock" }"""));

        Assert.True(_variables.TryGet("clock", out var value));
        Assert.Equal("14:05", value);
        Assert.True(Assert.Single(_shell.Ran).Captured);
    }

    [Fact]
    public async Task ShellDoesNotWaitWhenNobodyWantsTheOutput()
    {
        Assert.True(await Run("""{ "type": "shell", "command": "echo hi", "shell": "cmd" }"""));

        var ran = Assert.Single(_shell.Ran);
        Assert.Equal(ShellKind.Cmd, ran.Shell);
        Assert.False(ran.Captured);
    }

    [Fact]
    public async Task AWindowActionNeedsSomethingToMatchOn()
    {
        var message = await Fails("""{ "type": "window", "operation": "focus" }""");

        Assert.Contains("processName", message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AWindowThatIsNotOpenSaysSo()
    {
        var message = await Fails("""{ "type": "window", "operation": "close", "processName": "nothing.exe" }""");

        Assert.Contains("No open window matched", message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AWindowThatIsOpenIsActedOn()
    {
        _windows.Existing.Add("obs64.exe");

        Assert.True(await Run("""{ "type": "window", "operation": "minimise", "processName": "obs64.exe" }"""));

        Assert.Equal(WindowOperation.Minimise, Assert.Single(_windows.Applied).Operation);
    }

    [Fact]
    public async Task TheClipboardRoundTripsThroughAVariable()
    {
        Assert.True(await Run("""{ "type": "clipboard", "operation": "set", "value": "hello" }"""));
        Assert.True(await Run("""{ "type": "clipboard", "operation": "get", "into": "copied" }"""));

        Assert.True(_variables.TryGet("copied", out var value));
        Assert.Equal("hello", value);

        Assert.True(await Run("""{ "type": "clipboard", "operation": "clear" }"""));
        Assert.Null(_clipboard.GetText());
    }

    // ------------------------------------------------------------------ audio

    [Fact]
    public async Task AudioTogglesMuteOnTheDefaultDevice()
    {
        Assert.True(await Run("""{ "type": "audio", "operation": "toggleMute" }"""));

        Assert.True(_audio.Muted);
        Assert.Equal(AudioTarget.Default, Assert.Single(_audio.Applied).Target);
    }

    [Fact]
    public async Task AudioForOneProgramNeedsItsName()
    {
        var message = await Fails("""{ "type": "audio", "target": "process", "operation": "mute" }""");

        Assert.Contains("name", message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AudioAdjustsByAnAmount()
    {
        _audio.Volume = 0.5;

        Assert.True(await Run("""{ "type": "audio", "operation": "adjust", "value": -0.2 }"""));

        Assert.Equal(0.3, _audio.Volume, 3);
    }

    [Fact]
    public async Task MediaPressesTheRightKey()
    {
        Assert.True(await Run("""{ "type": "media", "command": "next" }"""));

        Assert.Equal(VirtualKey.MediaNextTrack, Assert.Single(_input.Sent).Key);
    }

    // ------------------------------------------------------------------ obs

    [Fact]
    public async Task ObsPassesTheCommandThrough()
    {
        Assert.True(await Run("""{ "type": "obs", "command": "setScene", "scene": "Starting soon" }"""));

        var request = Assert.Single(_obs.Sent);
        Assert.Equal(ObsCommand.SetScene, request.Command);
        Assert.Equal("Starting soon", request.Scene);
    }

    [Fact]
    public async Task ObsBeingClosedIsReportedNotThrown()
    {
        _obs.IsConnected = false;

        var message = await Fails("""{ "type": "obs", "command": "startStream" }""");

        Assert.Contains("OBS is not connected", message, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------ deck control

    [Fact]
    public async Task DeckControlReachesTheController()
    {
        Assert.True(await Run("""{ "type": "switchProfile", "profile": "streaming" }"""));
        Assert.True(await Run("""{ "type": "switchPage", "page": "scenes" }"""));
        Assert.True(await Run("""{ "type": "switchPage", "page": "next" }"""));
        Assert.True(await Run("""{ "type": "switchPage", "page": "previous" }"""));
        Assert.True(await Run("""{ "type": "openFolder", "page": "scenes" }"""));
        Assert.True(await Run("""{ "type": "closeFolder" }"""));

        Assert.Equal(
            new[] { "profile:streaming", "page:scenes", "next", "previous", "open:scenes", "close" },
            _deck.Moves);
    }

    // ------------------------------------------------------------------ variables

    [Fact]
    public async Task SettingAndFlippingAVariable()
    {
        Assert.True(await Run("""{ "type": "setVariable", "name": "mode", "value": "quiet" }"""));
        Assert.True(_variables.TryGet("mode", out var mode));
        Assert.Equal("quiet", mode);

        Assert.True(await Run("""{ "type": "toggleVariable", "name": "live" }"""));
        Assert.True(_variables.TryGet("live", out var live));
        Assert.Equal("true", live);

        Assert.True(await Run("""{ "type": "toggleVariable", "name": "live" }"""));
        Assert.True(_variables.TryGet("live", out live));
        Assert.Equal("false", live);
    }

    [Fact]
    public async Task AVariableCanBeWrittenWithOrWithoutItsPrefix()
    {
        Assert.True(await Run("""{ "type": "setVariable", "name": "var.mode", "value": "loud" }"""));

        Assert.True(_variables.TryGet("mode", out var value));
        Assert.Equal("loud", value);
    }

    // ------------------------------------------------------------------ composition

    [Fact]
    public async Task ASequenceRunsItsStepsInOrder()
    {
        Assert.True(await Run("""
        { "type": "sequence", "steps": [
            { "type": "setVariable", "name": "step", "value": "one" },
            { "type": "text", "value": "hello" },
            { "type": "setVariable", "name": "step", "value": "two" }
        ] }
        """));

        Assert.Equal("hello", Assert.Single(_input.Typed));
        Assert.True(_variables.TryGet("step", out var step));
        Assert.Equal("two", step);
    }

    [Fact]
    public async Task ASequenceStopsAtABadStepByDefault()
    {
        Assert.True(await Run("""
        { "type": "sequence", "steps": [
            { "type": "hotkey", "keys": "ctrl+nope" },
            { "type": "text", "value": "never" }
        ] }
        """));

        Assert.Empty(_input.Typed);
    }

    [Fact]
    public async Task ASequenceCanBeToldToCarryOn()
    {
        Assert.True(await Run("""
        { "type": "sequence", "stopOnError": false, "steps": [
            { "type": "hotkey", "keys": "ctrl+nope" },
            { "type": "text", "value": "still ran" }
        ] }
        """));

        Assert.Equal("still ran", Assert.Single(_input.Typed));
    }

    [Fact]
    public async Task AnEmptySequenceSaysSo()
    {
        var message = await Fails("""{ "type": "sequence", "steps": [] }""");

        Assert.Contains("no steps", message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ADelayActuallyWaits()
    {
        var started = DateTime.UtcNow;

        Assert.True(await Run("""{ "type": "delay", "ms": 60 }"""));

        Assert.True(DateTime.UtcNow - started >= TimeSpan.FromMilliseconds(40));
    }

    [Fact]
    public async Task AConditionalTakesTheThenBranch()
    {
        _variables.Set("mode", "quiet", VariableScope.Session);

        Assert.True(await Run("""
        { "type": "conditional", "if": "var.mode == \"quiet\"",
          "then": { "type": "text", "value": "was quiet" },
          "else": { "type": "text", "value": "was not" } }
        """));

        Assert.Equal("was quiet", Assert.Single(_input.Typed));
    }

    [Fact]
    public async Task AConditionalTakesTheElseBranch()
    {
        Assert.True(await Run("""
        { "type": "conditional", "if": "var.mode == \"quiet\"",
          "then": { "type": "text", "value": "was quiet" },
          "else": { "type": "text", "value": "was not" } }
        """));

        Assert.Equal("was not", Assert.Single(_input.Typed));
    }

    [Fact]
    public async Task AConditionalWithOnlyAThenDoesNothingWhenItDoesNotHold()
    {
        Assert.True(await Run("""
        { "type": "conditional", "if": "false", "then": { "type": "text", "value": "no" } }
        """));

        Assert.Empty(_input.Typed);
    }

    [Fact]
    public async Task ABadConditionSaysWhatIsWrongWithIt()
    {
        var message = await Fails("""{ "type": "conditional", "if": "(true", "then": { "type": "delay" } }""");

        Assert.Contains("never closed", message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RandomPicksOneOfTheChoices()
    {
        Assert.True(await Run("""
        { "type": "random", "from": [
            { "type": "text", "value": "gg" },
            { "type": "text", "value": "well played" }
        ] }
        """));

        var typed = Assert.Single(_input.Typed);
        Assert.Contains(typed, new[] { "gg", "well played" });
    }

    // ------------------------------------------------------------------ escape hatches

    [Fact]
    public async Task HttpSendsAndCanKeepTheAnswer()
    {
        _http.Response = "{\"on\":true}";

        Assert.True(await Run("""
        { "type": "http", "method": "POST", "url": "http://localhost/api",
          "body": "{}", "saveResponseTo": "light" }
        """));

        var sent = Assert.Single(_http.Sent);
        Assert.Equal("POST", sent.Method);
        Assert.True(_variables.TryGet("light", out var answer));
        Assert.Equal("{\"on\":true}", answer);
    }

    [Fact]
    public async Task AutoHotkeyRunsTheSnippet()
    {
        Assert.True(await Run("""{ "type": "ahk", "script": "MsgBox 'hi'" }"""));

        Assert.Equal("MsgBox 'hi'", Assert.Single(_scripts.Ran));
    }

    [Fact]
    public async Task AutoHotkeyMissingIsExplainedNotThrown()
    {
        _scripts.IsAutoHotkeyInstalled = false;

        var message = await Fails("""{ "type": "ahk", "script": "MsgBox 'hi'" }""");

        Assert.Contains("not installed", message, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------ the set itself

    [Fact]
    public void EveryActionInTheBriefExists()
    {
        var registry = ActionRegistry.Scan(Logger.None, typeof(HotkeyAction).Assembly);

        var expected = new[]
        {
            "hotkey", "keyDown", "keyUp", "text", "mouse", "scroll",
            "launch", "open", "shell", "window", "clipboard",
            "audio", "media", "obs",
            "switchProfile", "switchPage", "openFolder", "closeFolder", "setVariable", "toggleVariable",
            "sequence", "delay", "conditional", "random",
            "http", "ahk",
        };

        Assert.Equal(
            Array.Empty<string>(),
            expected.Where(type => !registry.KnownTypes.Contains(type)).ToArray());
    }

    [Fact]
    public void EveryActionSaysWhatItIsForThePicker()
    {
        var registry = ActionRegistry.Scan(Logger.None, typeof(HotkeyAction).Assembly);

        foreach (var action in registry.All)
        {
            Assert.False(string.IsNullOrWhiteSpace(action.Title), $"{action.Type} has no title.");
            Assert.False(string.IsNullOrWhiteSpace(action.Description), $"{action.Type} has no description.");
        }
    }
}
