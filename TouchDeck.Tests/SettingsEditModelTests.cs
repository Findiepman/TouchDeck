using System.Reflection;
using TouchDeck.App.Configurator;
using TouchDeck.Core.Configuration;
using TouchDeck.Platform.Display;
using Xunit;

namespace TouchDeck.Tests;

/// <summary>
/// A switch offered in the settings panel has to reach the file. The editor writes the
/// config back field by field, so one that is shown but never listed there looks like it
/// changed, saves, and comes back exactly as it was.
/// </summary>
public sealed class SettingsEditModelTests
{
    private static readonly IReadOnlyList<MonitorInfo> NoMonitors = Array.Empty<MonitorInfo>();

    [Fact]
    public void EveryBehaviourSwitchTheEditorOffersIsActuallyWritten()
    {
        // Every boolean the config has and the editor exposes under the same name, flipped
        // on the editor and read back off the config it produces. A property that is bound
        // in the panel but forgotten in ToConfig fails here, which is the one way this can
        // go wrong quietly. It guards the next switch somebody adds as well as today's.
        var editor = new SettingsEditModel(new AppConfig(), NoMonitors);

        var flags = typeof(BehaviourConfig)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.PropertyType == typeof(bool))
            .Select(property => (Config: property, Editor: Offered(property.Name)))
            .Where(pair => pair.Editor is not null)
            .ToArray();

        Assert.NotEmpty(flags);

        foreach (var (_, offered) in flags)
        {
            offered!.SetValue(editor, !(bool)offered.GetValue(editor)!);
        }

        var written = editor.ToConfig().Behaviour;

        foreach (var (setting, offered) in flags)
        {
            Assert.Equal(offered!.GetValue(editor), setting.GetValue(written));
        }

        static PropertyInfo? Offered(string name) =>
            typeof(SettingsEditModel).GetProperty(name, BindingFlags.Public | BindingFlags.Instance) is
                { CanRead: true, CanWrite: true } property && property.PropertyType == typeof(bool)
                ? property
                : null;
    }

    [Fact]
    public void TapsAreKeptOffTheMousePointerUnlessSomebodySaysOtherwise()
    {
        Assert.True(new BehaviourConfig().ClaimTouchInput);
    }

    [Fact]
    public void TurningTapsBackIntoMouseClicksIsRememberedRatherThanReset()
    {
        var config = new AppConfig { Behaviour = new BehaviourConfig { ClaimTouchInput = false } };
        var editor = new SettingsEditModel(config, NoMonitors);

        Assert.False(editor.ClaimTouchInput);
        Assert.False(editor.ToConfig().Behaviour.ClaimTouchInput);

        editor.ClaimTouchInput = true;

        Assert.True(editor.ToConfig().Behaviour.ClaimTouchInput);
    }
}
