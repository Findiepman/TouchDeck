using Microsoft.Win32;
using Serilog.Core;
using TouchDeck.Platform.Services;
using Xunit;

namespace TouchDeck.Tests;

/// <summary>
/// Starting with Windows, against the real registry. The entry is put back exactly as it
/// was found, so running the tests never changes what starts on this machine.
/// </summary>
public sealed class StartupTests : IDisposable
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    private readonly string? _before;

    public StartupTests()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
        _before = key?.GetValue(WindowsStartup.EntryName) as string;
    }

    public void Dispose()
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);

        if (key is null)
        {
            return;
        }

        if (_before is null)
        {
            key.DeleteValue(WindowsStartup.EntryName, throwOnMissingValue: false);
        }
        else
        {
            key.SetValue(WindowsStartup.EntryName, _before, RegistryValueKind.String);
        }
    }

    private static WindowsStartup Startup => new(Logger.None);

    [Fact]
    public void TurningItOnAddsAnEntryAndTurningItOffRemovesIt()
    {
        var startup = Startup;
        var command = WindowsStartup.CommandFor(@"C:\Program Files\TouchDeck\TouchDeck.exe", null);

        Assert.True(startup.Apply(enabled: true, command));
        Assert.Equal(command, startup.CurrentCommand);

        Assert.True(startup.Apply(enabled: false, command));
        Assert.Null(startup.CurrentCommand);
    }

    [Fact]
    public void ApplyingTheSameSettingTwiceChangesNothingTheSecondTime()
    {
        var startup = Startup;
        var command = WindowsStartup.CommandFor(@"C:\TouchDeck\TouchDeck.exe", null);

        Assert.True(startup.Apply(enabled: true, command));
        Assert.False(startup.Apply(enabled: true, command));

        Assert.True(startup.Apply(enabled: false, command));
        Assert.False(startup.Apply(enabled: false, command));
    }

    [Fact]
    public void MovingTheDeckRewritesThePath()
    {
        var startup = Startup;

        startup.Apply(enabled: true, WindowsStartup.CommandFor(@"C:\Old\TouchDeck.exe", null));
        var moved = WindowsStartup.CommandFor(@"C:\New\TouchDeck.exe", null);

        Assert.True(startup.Apply(enabled: true, moved));
        Assert.Equal(moved, startup.CurrentCommand);
    }

    [Fact]
    public void APathWithSpacesIsQuoted() =>
        Assert.Equal(
            "\"C:\\Program Files\\TouchDeck\\TouchDeck.exe\"",
            WindowsStartup.CommandFor(@"C:\Program Files\TouchDeck\TouchDeck.exe", null));

    [Fact]
    public void ACustomConfigFolderIsCarriedThrough() =>
        Assert.Equal(
            "\"C:\\TouchDeck\\TouchDeck.exe\" --config \"D:\\My Decks\"",
            WindowsStartup.CommandFor(@"C:\TouchDeck\TouchDeck.exe", @"D:\My Decks"));
}
