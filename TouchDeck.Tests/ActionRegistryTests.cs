using System.Text.Json;
using Serilog.Core;
using TouchDeck.Actions;
using TouchDeck.Core.Abstractions;
using TouchDeck.Core.Actions;
using TouchDeck.Core.Configuration;
using TouchDeck.Core.Input;
using Xunit;

namespace TouchDeck.Tests;

public class ActionRegistryTests
{
    private static ActionRegistry Registry => ActionRegistry.Scan(Logger.None, typeof(HotkeyAction).Assembly);

    [Fact]
    public void ScanningFindsEveryActionInTheAssembly()
    {
        var registry = Registry;

        Assert.Contains("hotkey", registry.KnownTypes);
        Assert.Contains("launch", registry.KnownTypes);
    }

    [Fact]
    public void LookupIsCaseInsensitive()
    {
        Assert.True(Registry.TryGet("HOTKEY", out var action));
        Assert.Equal("hotkey", action!.Type);
    }

    [Fact]
    public void AnUnknownTypeIsNotFound()
    {
        Assert.False(Registry.TryGet("teleport", out _));
    }

    [Fact]
    public async Task TheHotkeyActionSendsTheComboItWasGiven()
    {
        var injector = new RecordingInjector();
        var dispatcher = DispatcherWith(injector);

        var ran = await dispatcher.ExecuteAsync(Action("""{ "type": "hotkey", "keys": "ctrl+shift+m" }"""), default);

        Assert.True(ran);
        var combo = Assert.Single(injector.Sent);
        Assert.Equal(
            new[] { VirtualKey.LeftControl, VirtualKey.LeftShift, VirtualKey.M },
            combo.PressOrder);
    }

    [Fact]
    public async Task RepeatSendsTheComboMoreThanOnce()
    {
        var injector = new RecordingInjector();
        var dispatcher = DispatcherWith(injector);

        await dispatcher.ExecuteAsync(Action("""{ "type": "hotkey", "keys": "volumeup", "repeat": 3 }"""), default);

        Assert.Equal(3, injector.Sent.Count);
    }

    [Fact]
    public async Task AnUnparsableComboFailsWithoutSendingAnything()
    {
        var injector = new RecordingInjector();
        var dispatcher = DispatcherWith(injector);
        ActionFailure? failure = null;
        dispatcher.Failed += (_, f) => failure = f;

        var ran = await dispatcher.ExecuteAsync(Action("""{ "type": "hotkey", "keys": "ctrl+nope" }"""), default);

        Assert.False(ran);
        Assert.Empty(injector.Sent);
        Assert.Contains("not a key name", failure!.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AMissingRequiredParameterIsReportedRatherThanThrown()
    {
        var dispatcher = DispatcherWith(new RecordingInjector());
        ActionFailure? failure = null;
        dispatcher.Failed += (_, f) => failure = f;

        var ran = await dispatcher.ExecuteAsync(Action("""{ "type": "hotkey" }"""), default);

        Assert.False(ran);
        Assert.Contains("keys", failure!.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AnUnknownActionTypeIsReportedRatherThanThrown()
    {
        var dispatcher = DispatcherWith(new RecordingInjector());
        ActionFailure? failure = null;
        dispatcher.Failed += (_, f) => failure = f;

        var ran = await dispatcher.ExecuteAsync(Action("""{ "type": "teleport" }"""), default);

        Assert.False(ran);
        Assert.Contains("teleport", failure!.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OneFailingActionDoesNotStopTheNextOne()
    {
        var injector = new RecordingInjector();
        var dispatcher = DispatcherWith(injector);

        await dispatcher.ExecuteAsync(Action("""{ "type": "hotkey", "keys": "ctrl+nope" }"""), default);
        var second = await dispatcher.ExecuteAsync(Action("""{ "type": "hotkey", "keys": "a" }"""), default);

        Assert.True(second);
        Assert.Single(injector.Sent);
    }

    private static ActionDispatcher DispatcherWith(IInputInjector injector)
    {
        var services = new ServiceRegistry()
            .Add<IInputInjector>(injector)
            .Add<IProcessLauncher>(new RecordingLauncher());

        return new ActionDispatcher(Registry, services, Logger.None);
    }

    private static ActionConfig Action(string json) =>
        JsonSerializer.Deserialize<ActionConfig>(json, ConfigJson.Options)!;

}
