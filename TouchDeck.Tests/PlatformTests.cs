using System.IO;
using Serilog.Core;
using TouchDeck.Core.Abstractions;
using TouchDeck.Core.Expressions;
using TouchDeck.Core.Variables;
using TouchDeck.Platform.Services;
using TouchDeck.Platform.Windowing;
using Xunit;

namespace TouchDeck.Tests;

/// <summary>
/// The platform code that is easy to get subtly wrong, exercised for real rather than
/// through a fake: the clipboard interop, variable persistence, and window lookup.
/// </summary>
public sealed class PlatformTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "touchdeck-tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public void TheClipboardRoundTripsRealText()
    {
        var clipboard = new Win32Clipboard(Logger.None);
        var before = clipboard.GetText();

        try
        {
            var written = $"touchdeck round trip {Guid.NewGuid():N}";
            clipboard.SetText(written);

            Assert.Equal(written, clipboard.GetText());
        }
        finally
        {
            // Whatever the developer had on the clipboard goes back.
            if (before is not null)
            {
                clipboard.SetText(before);
            }
        }
    }

    [Fact]
    public void TheClipboardHandlesTextWithAwkwardCharacters()
    {
        var clipboard = new Win32Clipboard(Logger.None);
        var before = clipboard.GetText();

        try
        {
            const string written = "line one\r\nline two\ttabbed é ü 😀";
            clipboard.SetText(written);

            Assert.Equal(written, clipboard.GetText());
        }
        finally
        {
            if (before is not null)
            {
                clipboard.SetText(before);
            }
        }
    }

    [Fact]
    public void PersistentVariablesSurviveARestart()
    {
        var path = Path.Combine(_root, "variables.json");

        var first = new VariableStore(path);
        first.Set("mode", "quiet", VariableScope.Persistent);
        first.Set("temporary", "gone", VariableScope.Session);

        var second = new VariableStore(path);

        Assert.True(second.TryGet("mode", out var mode));
        Assert.Equal("quiet", mode);
        Assert.False(second.TryGet("temporary", out _));
    }

    [Fact]
    public void ClearingAVariableRemovesItFromDiskToo()
    {
        var path = Path.Combine(_root, "variables.json");

        var first = new VariableStore(path);
        first.Set("mode", "quiet", VariableScope.Persistent);
        first.Set("mode", null, VariableScope.Persistent);

        Assert.False(new VariableStore(path).TryGet("mode", out _));
    }

    [Fact]
    public void ATogglePersistsWhenTheVariableWasPersistent()
    {
        var path = Path.Combine(_root, "variables.json");

        var first = new VariableStore(path);
        first.Set("live", "false", VariableScope.Persistent);
        Assert.True(first.Toggle("live"));

        Assert.True(new VariableStore(path).TryGet("live", out var value));
        Assert.Equal("true", value);
    }

    [Fact]
    public void ACorruptVariablesFileIsIgnoredRatherThanFatal()
    {
        var path = Path.Combine(_root, "variables.json");
        Directory.CreateDirectory(_root);
        File.WriteAllText(path, "{ this is not json");

        var store = new VariableStore(path);

        Assert.Empty(store.Snapshot());
    }

    [Fact]
    public void AVariableChangeIsAnnounced()
    {
        var store = new VariableStore();
        var announced = 0;
        store.Changed += (_, _) => announced++;

        store.Set("a", "1", VariableScope.Session);
        store.Toggle("b");

        Assert.Equal(2, announced);
    }

    [Fact]
    public void TheForegroundProcessCanBeRead()
    {
        // Whatever is in front while the tests run, asking must not throw.
        var name = new WindowManager(Logger.None).ForegroundProcessName;

        Assert.True(name is null || name.Length > 0);
    }

    [Fact]
    public void LookingForAWindowThatCannotExistFindsNothing()
    {
        var windows = new WindowManager(Logger.None);

        Assert.Null(windows.Find(new WindowMatch("touchdeck-no-such-program", null)));
        Assert.False(windows.Apply(WindowOperation.Focus, new WindowMatch("touchdeck-no-such-program", null)));
    }

    [Fact]
    public void AMatchWithNothingToMatchOnFindsNothing() =>
        Assert.Null(new WindowManager(Logger.None).Find(new WindowMatch(null, null)));

    [Fact]
    public void ABrokenTitlePatternIsReportedRatherThanThrown() =>
        Assert.Null(new WindowManager(Logger.None).Find(new WindowMatch(null, "([unclosed")));

    [Fact]
    public void AutoHotkeyReportsWhetherItIsThere()
    {
        // Either answer is correct; what matters is that looking does not throw.
        var runner = new AutoHotkeyRunner(Logger.None);

        Assert.True(runner.IsAutoHotkeyInstalled || !runner.IsAutoHotkeyInstalled);
    }

    [Fact]
    public void ConditionsReadRealVariables()
    {
        var store = new VariableStore(Path.Combine(_root, "variables.json"));
        store.Set("mode", "quiet", VariableScope.Persistent);
        store.Set("cpu", "91.5", VariableScope.Session);

        var resolver = new DeckValueResolver(store);

        Assert.True(Expression.Parse("var.mode == \"quiet\" and var.cpu > 80").Evaluate(resolver));
        Assert.False(Expression.Parse("var.mode == \"loud\"").Evaluate(resolver));
    }
}
