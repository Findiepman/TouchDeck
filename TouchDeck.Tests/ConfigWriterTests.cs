using System.IO;
using TouchDeck.Core.Configuration;
using Xunit;

namespace TouchDeck.Tests;

public sealed class ConfigWriterTests : IDisposable
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
    public void WhatIsWrittenLoadsBackTheSame()
    {
        StarterConfig.EnsureExists(Paths);
        var before = new ConfigLoader(Paths, KnownActions.Types).Load();

        var writer = new ConfigWriter(Paths);
        writer.SaveAppConfig(before.App);
        writer.SaveThemes(before.Themes);
        foreach (var profile in before.Profiles)
        {
            writer.SaveProfile(profile);
        }

        var after = new ConfigLoader(Paths, KnownActions.Types).Load();

        Assert.Empty(after.Messages);
        Assert.Equal(before.App.Behaviour, after.App.Behaviour);
        Assert.Equal(before.App.Display, after.App.Display);
        Assert.Equal(before.Profiles.Count, after.Profiles.Count);
        Assert.Equal(
            before.Profiles.Select(p => p.Id),
            after.Profiles.Select(p => p.Id));
        Assert.Equal(
            before.Profiles[0].Pages[0].Buttons.Select(b => b.Label),
            after.Profiles[0].Pages[0].Buttons.Select(b => b.Label));
    }

    [Fact]
    public void SavingKeepsTheHotkeyReadable()
    {
        StarterConfig.EnsureExists(Paths);
        var loaded = new ConfigLoader(Paths).Load();

        new ConfigWriter(Paths).SaveProfile(loaded.FindProfile("default")!);

        var text = File.ReadAllText(Path.Combine(Paths.ProfilesDirectory, "default.json"));

        Assert.Contains("ctrl+c", text, StringComparison.Ordinal);
        Assert.DoesNotContain("\\u002B", text, StringComparison.Ordinal);
    }

    [Fact]
    public void SavingKeepsTheSchemaReference()
    {
        StarterConfig.EnsureExists(Paths);
        var loaded = new ConfigLoader(Paths).Load();

        new ConfigWriter(Paths).SaveProfile(loaded.FindProfile("default")!);

        var text = File.ReadAllText(Path.Combine(Paths.ProfilesDirectory, "default.json"));

        Assert.Contains("$schema", text, StringComparison.Ordinal);
    }

    [Fact]
    public void AnUnchangedFileIsNotRewritten()
    {
        StarterConfig.EnsureExists(Paths);
        var loaded = new ConfigLoader(Paths).Load();
        var writer = new ConfigWriter(Paths);

        Assert.True(writer.SaveProfile(loaded.FindProfile("default")!));
        Assert.False(writer.SaveProfile(loaded.FindProfile("default")!));
    }

    [Fact]
    public void ThePreviousVersionIsKeptAsABackup()
    {
        StarterConfig.EnsureExists(Paths);
        var loaded = new ConfigLoader(Paths).Load();
        var profile = loaded.FindProfile("default")!;

        new ConfigWriter(Paths).SaveProfile(profile with { Name = "Renamed" });

        var backup = Path.Combine(Paths.ProfilesDirectory, "default.json" + ConfigWriter.BackupExtension);

        Assert.True(File.Exists(backup));
        Assert.Contains("\"Default\"", File.ReadAllText(backup), StringComparison.Ordinal);
    }

    [Fact]
    public void DeletingAProfileKeepsABackupOfIt()
    {
        StarterConfig.EnsureExists(Paths);
        var loaded = new ConfigLoader(Paths).Load();
        var profile = loaded.FindProfile("media")!;

        new ConfigWriter(Paths).DeleteProfile(profile);

        Assert.False(File.Exists(Path.Combine(Paths.ProfilesDirectory, "media.json")));
        Assert.True(File.Exists(Path.Combine(Paths.ProfilesDirectory, "media.json" + ConfigWriter.BackupExtension)));
    }

    [Theory]
    [InlineData("my profile", "my profile")]
    [InlineData("a/b:c", "a-b-c")]
    [InlineData("", "profile")]
    public void ProfileIdsBecomeSafeFileNames(string id, string expected) =>
        Assert.Equal(expected, ConfigWriter.Sanitise(id));
}
