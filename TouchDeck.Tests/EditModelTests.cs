using System.Text.Json;
using Serilog.Core;
using TouchDeck.Actions;
using TouchDeck.App.Configurator;
using TouchDeck.Core.Actions;
using TouchDeck.Core.Configuration;
using Xunit;

namespace TouchDeck.Tests;

public class EditModelTests
{
    private static ActionRegistry Registry => ActionRegistry.Scan(Logger.None, typeof(HotkeyAction).Assembly);

    private static ActionConfig Action(string json) =>
        JsonSerializer.Deserialize<ActionConfig>(json, ConfigJson.Options)!;

    [Fact]
    public void AnActionKeepsItsValuesThroughTheEditor()
    {
        var model = new ActionEditModel(Registry, Action("""{ "type": "hotkey", "keys": "ctrl+c", "repeat": 2 }"""));

        Assert.Equal("hotkey", model.Type);
        Assert.Equal("ctrl+c", model.Parameters.Single(p => p.Name == "keys").Value);
        Assert.Equal("2", model.Parameters.Single(p => p.Name == "repeat").Value);

        var written = model.ToConfig();

        Assert.Equal("ctrl+c", written.GetString("keys"));
        Assert.Equal(2, written.GetInt32("repeat"));
    }

    [Fact]
    public void TheEditorOffersTheParametersTheActionDeclares()
    {
        var model = new ActionEditModel(Registry, Action("""{ "type": "launch", "path": "notepad.exe" }"""));

        Assert.Equal(
            new[] { "path", "args", "workingDir", "focusIfRunning", "singleInstance" },
            model.Parameters.Select(p => p.Name));
        Assert.True(model.Parameters.Single(p => p.Name == "path").IsRequired);
        Assert.False(model.Parameters.Single(p => p.Name == "args").IsRequired);
    }

    [Fact]
    public void AnEmptyParameterIsLeftOutRatherThanWrittenAsBlank()
    {
        var model = new ActionEditModel(Registry, Action("""{ "type": "launch", "path": "notepad.exe" }"""));

        var written = model.ToConfig();

        Assert.False(written.TryGetParameter("args", out _));
    }

    [Fact]
    public void ChangingTheTypeSwapsInThatTypesParameters()
    {
        var model = new ActionEditModel(Registry, Action("""{ "type": "hotkey", "keys": "ctrl+c" }"""))
        {
            Type = "launch",
        };

        Assert.Equal(
            new[] { "path", "args", "workingDir", "focusIfRunning", "singleInstance" },
            model.Parameters.Select(p => p.Name));
        Assert.Equal("launch", model.ToConfig().Type);
    }

    [Fact]
    public void ParametersTheActionDoesNotKnowAboutAreKept()
    {
        var model = new ActionEditModel(
            Registry,
            Action("""{ "type": "hotkey", "keys": "ctrl+c", "writtenByHand": "keep me" }"""));

        var written = model.ToConfig();

        Assert.Equal("keep me", written.GetString("writtenByHand"));
    }

    [Fact]
    public void AButtonRoundTripsThroughTheEditor()
    {
        var original = new ButtonConfig
        {
            Col = 2,
            Row = 1,
            ColSpan = 2,
            Label = "Clip",
            Style = new ButtonStyle { Background = "#5A1D1D" },
            Action = Action("""{ "type": "hotkey", "keys": "alt+f10" }"""),
        };

        var written = new ButtonEditModel(Registry, original).ToConfig();

        Assert.Equal(2, written.Col);
        Assert.Equal(1, written.Row);
        Assert.Equal(2, written.ColSpan);
        Assert.Null(written.RowSpan);
        Assert.Equal("Clip", written.Label);
        Assert.Equal("#5A1D1D", written.Style!.Background);
        Assert.Equal("alt+f10", written.Action!.GetString("keys"));
    }

    [Fact]
    public void AButtonWithNoStyleOverridesWritesNoStyleBlock()
    {
        var model = new ButtonEditModel(Registry, new ButtonConfig { Label = "Plain" });

        Assert.Null(model.ToConfig().Style);
    }

    [Fact]
    public void ClearingAStyleBoxRemovesTheOverride()
    {
        var model = new ButtonEditModel(
            Registry,
            new ButtonConfig { Style = new ButtonStyle { Background = "#111111" } });

        model.Style.Background = "   ";

        Assert.Null(model.ToConfig().Style);
    }

    [Fact]
    public void AProfileRoundTripsThroughTheEditor()
    {
        var original = new Profile
        {
            Id = "streaming",
            Name = "Streaming",
            Theme = "dark",
            Grid = new GridConfig { Columns = 4, Rows = 2 },
            Pages = new[]
            {
                new Page
                {
                    Id = "main",
                    Buttons = new[]
                    {
                        new ButtonConfig { Col = 0, Row = 0, Action = Action("""{ "type": "hotkey", "keys": "a" }""") },
                    },
                },
            },
        };

        var written = new ProfileEditModel(Registry, original).ToConfig();

        Assert.Equal("streaming", written.Id);
        Assert.Equal(4, written.Grid.Columns);
        Assert.Equal("main", written.Pages[0].Id);
        Assert.Single(written.Pages[0].Buttons);
    }

    [Fact]
    public void EditingSomethingRaisesEditedOnTheWholeProfile()
    {
        var profile = new ProfileEditModel(Registry, new Profile
        {
            Id = "p",
            Pages = new[] { new Page { Id = "main", Buttons = new[] { new ButtonConfig { Label = "x" } } } },
        });

        var edited = false;
        profile.Edited += (_, _) => edited = true;

        profile.Pages[0].Buttons[0].Label = "y";

        Assert.True(edited);
    }
}
