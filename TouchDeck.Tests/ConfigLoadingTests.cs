using System.IO;
using TouchDeck.Core.Configuration;
using Xunit;

namespace TouchDeck.Tests;

public sealed class ConfigLoadingTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "touchdeck-tests", Guid.NewGuid().ToString("N"));

    private ConfigPaths Paths => new(_root);

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public void TheStarterConfigLoadsWithoutErrorsOrWarnings()
    {
        StarterConfig.EnsureExists(Paths);

        var configuration = new ConfigLoader(Paths, KnownActions.Types).Load();

        Assert.Empty(configuration.Messages);
        Assert.Equal(2, configuration.Profiles.Count);
        Assert.NotNull(configuration.FindProfile("default"));
        Assert.Equal("Default", configuration.StartupProfile!.DisplayName);
    }

    [Fact]
    public void TheStarterConfigIsWrittenOnlyOnce()
    {
        var first = StarterConfig.EnsureExists(Paths);
        var second = StarterConfig.EnsureExists(Paths);

        Assert.Equal(4, first.Count);
        Assert.Empty(second);
    }

    [Fact]
    public void AProfileWithOnlyAGridAndOneButtonWorks()
    {
        WriteProfile("minimal", """
        {
          "id": "minimal",
          "grid": { "columns": 2, "rows": 1 },
          "pages": [ { "id": "main", "buttons": [
            { "col": 0, "row": 0, "action": { "type": "hotkey", "keys": "a" } }
          ] } ]
        }
        """);

        var configuration = new ConfigLoader(Paths, new[] { "hotkey" }).Load();

        Assert.DoesNotContain(configuration.Messages, m => m.Severity == ValidationSeverity.Error);
        Assert.Single(configuration.Profiles);
        Assert.Equal(2, configuration.Profiles[0].Grid.Columns);
    }

    [Fact]
    public void BrokenJsonIsReportedWithFileAndLineAndDoesNotThrow()
    {
        WriteProfile("broken", "{ \"id\": \"broken\", \n \"grid\": { \"columns\": } }");

        var configuration = new ConfigLoader(Paths).Load();

        var error = Assert.Single(
            configuration.Messages,
            m => m.Severity == ValidationSeverity.Error && m.File.Contains("broken", StringComparison.Ordinal));
        Assert.Contains("line", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AButtonOffTheEdgeOfTheGridIsAnError()
    {
        WriteProfile("overflow", """
        {
          "id": "overflow",
          "grid": { "columns": 2, "rows": 2 },
          "pages": [ { "id": "main", "buttons": [
            { "col": 2, "row": 0, "action": { "type": "hotkey", "keys": "a" } }
          ] } ]
        }
        """);

        var configuration = new ConfigLoader(Paths).Load();

        Assert.Contains(
            configuration.Messages,
            m => m.Severity == ValidationSeverity.Error && m.Message.Contains("does not fit", StringComparison.Ordinal));
    }

    [Fact]
    public void TwoButtonsInTheSameCellIsAWarning()
    {
        WriteProfile("overlap", """
        {
          "id": "overlap",
          "grid": { "columns": 3, "rows": 1 },
          "pages": [ { "id": "main", "buttons": [
            { "col": 0, "row": 0, "colSpan": 2, "action": { "type": "hotkey", "keys": "a" } },
            { "col": 1, "row": 0, "action": { "type": "hotkey", "keys": "b" } }
          ] } ]
        }
        """);

        var configuration = new ConfigLoader(Paths).Load();

        Assert.Contains(
            configuration.Messages,
            m => m.Severity == ValidationSeverity.Warning && m.Message.Contains("already used", StringComparison.Ordinal));
    }

    [Fact]
    public void AnUnknownActionTypeIsAWarningAndNotAnError()
    {
        WriteProfile("unknown", """
        {
          "id": "unknown",
          "grid": { "columns": 1, "rows": 1 },
          "pages": [ { "id": "main", "buttons": [
            { "col": 0, "row": 0, "action": { "type": "teleport" } }
          ] } ]
        }
        """);

        var configuration = new ConfigLoader(Paths, new[] { "hotkey" }).Load();

        Assert.DoesNotContain(configuration.Messages, m => m.Severity == ValidationSeverity.Error);
        Assert.Contains(
            configuration.Messages,
            m => m.Message.Contains("teleport", StringComparison.Ordinal));
    }

    [Fact]
    public void AnActionWithoutATypeIsReportedRatherThanThrown()
    {
        WriteProfile("typeless", """
        {
          "id": "typeless",
          "grid": { "columns": 1, "rows": 1 },
          "pages": [ { "id": "main", "buttons": [
            { "col": 0, "row": 0, "action": { "keys": "a" } }
          ] } ]
        }
        """);

        var configuration = new ConfigLoader(Paths).Load();

        Assert.Contains(
            configuration.Messages,
            m => m.Severity == ValidationSeverity.Error && m.Message.Contains("type", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void DuplicateProfileIdsAreReported()
    {
        WriteProfile("one", """
        { "id": "same", "grid": { "columns": 1, "rows": 1 },
          "pages": [ { "id": "main", "buttons": [
            { "col": 0, "row": 0, "action": { "type": "hotkey", "keys": "a" } } ] } ] }
        """);
        WriteProfile("two", """
        { "id": "same", "grid": { "columns": 1, "rows": 1 },
          "pages": [ { "id": "main", "buttons": [
            { "col": 0, "row": 0, "action": { "type": "hotkey", "keys": "b" } } ] } ] }
        """);

        var configuration = new ConfigLoader(Paths).Load();

        Assert.Contains(
            configuration.Messages,
            m => m.Message.Contains("already uses the id", StringComparison.Ordinal));
    }

    [Fact]
    public void CommentsAndTrailingCommasAreAccepted()
    {
        WriteProfile("commented", """
        {
          // A comment, because these files are edited by hand.
          "id": "commented",
          "grid": { "columns": 1, "rows": 1 },
          "pages": [ { "id": "main", "buttons": [
            { "col": 0, "row": 0, "action": { "type": "hotkey", "keys": "a" } },
          ] } ],
        }
        """);

        var configuration = new ConfigLoader(Paths, new[] { "hotkey" }).Load();

        Assert.Empty(configuration.Messages);
    }

    [Fact]
    public void ActionsCarryTheJsonPathTheyCameFrom()
    {
        WriteProfile("paths", """
        {
          "id": "paths",
          "grid": { "columns": 2, "rows": 1 },
          "pages": [ { "id": "main", "buttons": [
            { "col": 0, "row": 0, "action": { "type": "hotkey", "keys": "a" } },
            { "col": 1, "row": 0, "action": { "type": "hotkey", "keys": "b" } }
          ] } ]
        }
        """);

        var configuration = new ConfigLoader(Paths, new[] { "hotkey" }).Load();

        Assert.Equal(
            "pages[0].buttons[1].action",
            configuration.Profiles[0].Pages[0].Buttons[1].Action!.JsonPath);
    }

    [Fact]
    public void MissingFilesProduceErrorsRatherThanAnException()
    {
        Paths.EnsureDirectories();

        var configuration = new ConfigLoader(Paths).Load();

        Assert.Contains(configuration.Messages, m => m.File.Contains("config.json", StringComparison.Ordinal));
        Assert.True(configuration.HasErrors);
        Assert.Equal(new AppConfig().Behaviour.DefaultProfile, configuration.App.Behaviour.DefaultProfile);
    }

    /// <summary>Writes one profile plus a config.json that points at it, so nothing else warns.</summary>
    private void WriteProfile(string name, string json)
    {
        var paths = Paths;
        paths.EnsureDirectories();
        File.WriteAllText(Path.Combine(paths.ProfilesDirectory, $"{name}.json"), json);
        File.WriteAllText(paths.ConfigFile, $$"""{ "behaviour": { "defaultProfile": "{{name}}" } }""");
    }
}
