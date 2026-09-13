using System.Text.Json;
using Serilog.Core;
using TouchDeck.Actions;
using TouchDeck.Core.Abstractions;
using TouchDeck.Core.Actions;
using TouchDeck.Core.Configuration;
using TouchDeck.Core.Expressions;
using TouchDeck.Core.Variables;
using TouchDeck.Platform.Audio;
using Xunit;

namespace TouchDeck.Tests;

/// <summary>
/// The soundboard action, driven through the real dispatcher. What it reads out of the JSON
/// and what it does when there is nothing to play.
/// </summary>
public sealed class SoundboardActionTests
{
    private readonly FakeSoundboard _player = new();
    private readonly ActionDispatcher _dispatcher;

    private string? _lastFailure;

    public SoundboardActionTests()
    {
        var variables = new VariableStore();
        var services = new ServiceRegistry()
            .Add<ISoundboardPlayer>(_player)
            .Add<IVariableStore>(variables)
            .Add<IValueResolver>(new DeckValueResolver(variables));

        _dispatcher = new ActionDispatcher(
            ActionRegistry.Scan(Logger.None, typeof(SoundboardAction).Assembly),
            services,
            Logger.None);

        _dispatcher.Failed += (_, failure) => _lastFailure = failure.Message;
    }

    private Task<bool> Run(string json) =>
        _dispatcher.ExecuteAsync(JsonSerializer.Deserialize<ActionConfig>(json, ConfigJson.Options)!, default);

    [Fact]
    public void TheActionIsDiscovered()
    {
        Assert.Contains("soundboard", KnownActions.Types);
    }

    [Fact]
    public async Task ACategoryIsEnoughToPlay()
    {
        Assert.True(await Run("""{ "type": "soundboard", "category": "insults" }"""));

        var request = Assert.Single(_player.Requests);
        Assert.Equal("insults", request.Category);
        Assert.Null(request.Id);
        Assert.Empty(request.Devices);
    }

    [Fact]
    public async Task TheDefaultsAreTheSoundboardOnes()
    {
        await Run("""{ "type": "soundboard", "category": "insults" }""");

        var request = Assert.Single(_player.Requests);
        Assert.Equal(SoundboardPolicy.Cutoff, request.Policy);
        Assert.Equal(0.9, request.Volume, 3);
    }

    [Fact]
    public async Task EveryParameterIsReadOff()
    {
        await Run(
            """
            {
              "type": "soundboard",
              "category": "insults",
              "id": "rage",
              "devices": ["CABLE Input", "Koptelefoon"],
              "volume": 0.5,
              "policy": "overlap"
            }
            """);

        var request = Assert.Single(_player.Requests);
        Assert.Equal("rage", request.Id);
        Assert.Equal(["CABLE Input", "Koptelefoon"], request.Devices);
        Assert.Equal(0.5, request.Volume, 3);
        Assert.Equal(SoundboardPolicy.Overlap, request.Policy);
    }

    [Fact]
    public async Task OneDeviceMayBeWrittenAsAPlainString()
    {
        await Run("""{ "type": "soundboard", "category": "hype", "devices": "CABLE Input" }""");

        Assert.Equal(["CABLE Input"], Assert.Single(_player.Requests).Devices);
    }

    [Theory]
    [InlineData("cutoff", SoundboardPolicy.Cutoff)]
    [InlineData("overlap", SoundboardPolicy.Overlap)]
    [InlineData("ignore", SoundboardPolicy.Ignore)]
    [InlineData("Overlap", SoundboardPolicy.Overlap)]
    public async Task EveryPolicyIsUnderstood(string written, SoundboardPolicy expected)
    {
        await Run($$"""{ "type": "soundboard", "category": "hype", "policy": "{{written}}" }""");

        Assert.Equal(expected, Assert.Single(_player.Requests).Policy);
    }

    [Fact]
    public async Task AnUnknownPolicyIsRejectedWithTheChoices()
    {
        Assert.False(await Run("""{ "type": "soundboard", "category": "hype", "policy": "louder" }"""));

        Assert.Contains("louder", _lastFailure);
        Assert.Contains("cutoff", _lastFailure);
        Assert.Empty(_player.Requests);
    }

    [Fact]
    public async Task ACategoryIsRequired()
    {
        Assert.False(await Run("""{ "type": "soundboard" }"""));

        Assert.Contains("category", _lastFailure);
        Assert.Empty(_player.Requests);
    }

    [Fact]
    public async Task AnIgnoredPressIsNotAFailure()
    {
        _player.Result = new SoundboardResult(SoundboardOutcome.Ignored, "still playing");

        Assert.True(await Run("""{ "type": "soundboard", "category": "hype" }"""));
        Assert.Null(_lastFailure);
    }

    [Theory]
    [InlineData(SoundboardOutcome.NoManifest)]
    [InlineData(SoundboardOutcome.NoCategory)]
    [InlineData(SoundboardOutcome.NothingPlayable)]
    [InlineData(SoundboardOutcome.NoSuchClip)]
    [InlineData(SoundboardOutcome.MissingFile)]
    [InlineData(SoundboardOutcome.DeviceUnavailable)]
    public async Task ARealProblemFlashesTheButtonInsteadOfThrowing(SoundboardOutcome outcome)
    {
        _player.Result = new SoundboardResult(outcome, "something specific went wrong");

        // False means the dispatcher caught it and raised Failed: the button shows an error
        // state and nothing escapes into the UI.
        Assert.False(await Run("""{ "type": "soundboard", "category": "hype" }"""));
        Assert.Equal("something specific went wrong", _lastFailure);
    }

    [Theory]
    [InlineData("default")]
    [InlineData("Default")]
    [InlineData("  DEFAULT  ")]
    public void TheWordDefaultStandsForWhateverWindowsIsPlayingThrough(string written) =>
        Assert.True(QuoteDeckSoundboard.IsDefault(written));

    [Theory]
    [InlineData("CABLE Input")]
    [InlineData("Sonar - Microphone")]
    [InlineData("defaults")]
    [InlineData("my default headset")]
    [InlineData("")]
    public void AnythingElseIsADeviceNameToMatch(string written) =>
        Assert.False(QuoteDeckSoundboard.IsDefault(written));

    [Fact]
    public void TheParametersAreDescribedForTheConfigCenter()
    {
        var action = new SoundboardAction();
        var names = action.Parameters.Select(parameter => parameter.Name).ToArray();

        Assert.Equal(["category", "id", "devices", "volume", "policy"], names);
        Assert.True(action.Parameters.Single(p => p.Name == "category").Required);
        Assert.Contains("cutoff", action.Parameters.Single(p => p.Name == "policy").Choices);
    }

    private sealed class FakeSoundboard : ISoundboardPlayer
    {
        public List<SoundboardRequest> Requests { get; } = [];

        public int StopCount { get; private set; }

        public SoundboardResult Result { get; set; } = new(SoundboardOutcome.Played);

        public SoundboardResult Play(SoundboardRequest request)
        {
            Requests.Add(request);
            return Result;
        }

        public void StopAll() => StopCount++;
    }
}
