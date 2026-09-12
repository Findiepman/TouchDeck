using System.IO;
using Serilog.Core;
using TouchDeck.Actions;
using TouchDeck.App.Configurator;
using TouchDeck.Core.Actions;
using TouchDeck.Core.Configuration;
using Xunit;

namespace TouchDeck.Tests;

/// <summary>
/// Covers what the config center does when you press things, without going near a window.
/// </summary>
public sealed class ConfiguratorTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "touchdeck-tests", Guid.NewGuid().ToString("N"));

    public ConfiguratorTests()
    {
        StarterConfig.EnsureExists(new ConfigPaths(_root));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private ConfiguratorViewModel Open() => new(
        new ConfigPaths(_root),
        ActionRegistry.Scan(Logger.None, typeof(HotkeyAction).Assembly),
        Logger.None);

    [Fact]
    public void ItOpensOnTheProfileTheDeckStartsWith()
    {
        var vm = Open();

        Assert.Equal("default", vm.CurrentProfile!.Id);
        Assert.Equal("main", vm.CurrentPage!.Id);
        Assert.False(vm.IsDirty);
        Assert.False(vm.HasProblems);
    }

    [Fact]
    public void NothingIsSelectedToBeginWith()
    {
        var vm = Open();

        Assert.False(vm.HasSelection);
        Assert.Null(vm.SelectedButton);
    }

    [Fact]
    public void PressingAnEmptySquareAddsAButtonThereAndSelectsIt()
    {
        var vm = Open();
        vm.CurrentProfile!.Rows = 4;

        vm.AddButtonAt(2, 3);

        var button = Assert.IsType<ButtonEditModel>(vm.Inspecting);
        Assert.Equal(2, button.Col);
        Assert.Equal(3, button.Row);
        Assert.Equal("hotkey", button.Action.Type);
        Assert.Contains(button, vm.CurrentPage!.Buttons);
        Assert.True(vm.IsDirty);
    }

    [Fact]
    public void ChoosingAnActionAddsAButtonRunningItInTheFirstFreeSquare()
    {
        var vm = Open();
        vm.CurrentProfile!.Rows = 4;
        var launch = vm.AvailableActions.Single(a => a.Type == "launch");

        vm.AddButtonWith(launch);

        var button = Assert.IsType<ButtonEditModel>(vm.Inspecting);
        Assert.Equal("launch", button.Action.Type);
        Assert.Equal(0, button.Col);
        Assert.Equal(3, button.Row);
        Assert.Equal("Launch program", button.Label);
    }

    [Fact]
    public void AFullPageSaysSoRatherThanFailingQuietly()
    {
        var vm = Open();

        vm.AddButtonWith(vm.AvailableActions.First());

        Assert.False(vm.HasSelection);
        Assert.Contains("Every cell is taken", vm.Status, StringComparison.Ordinal);
    }

    [Fact]
    public void DroppingAButtonOnAnotherTradesTheirPlaces()
    {
        var vm = Open();
        var copy = vm.CurrentPage!.Buttons.Single(b => b.Label == "Copy");
        var paste = vm.CurrentPage.Buttons.Single(b => b.Label == "Paste");
        var copyWas = (copy.Col, copy.Row);
        var pasteWas = (paste.Col, paste.Row);

        vm.Swap(copy, paste);

        Assert.Equal(pasteWas, (copy.Col, copy.Row));
        Assert.Equal(copyWas, (paste.Col, paste.Row));
        Assert.Same(copy, vm.SelectedButton);
    }

    [Fact]
    public void DeletingTheSelectedButtonRemovesItAndGoesBackToAdding()
    {
        var vm = Open();
        var target = vm.CurrentPage!.Buttons.Single(b => b.Label == "Snip");
        vm.Inspecting = target;

        vm.DeleteButtonCommand.Execute(null);

        Assert.DoesNotContain(target, vm.CurrentPage.Buttons);
        Assert.False(vm.HasSelection);
    }

    [Fact]
    public void CopyingBringsTheActionWithIt()
    {
        var vm = Open();
        var target = vm.CurrentPage!.Buttons.Single(b => b.Label == "Copy");
        vm.Inspecting = target;
        vm.DeleteButtonCommand.Execute(null);
        vm.Inspecting = vm.CurrentPage.Buttons.Single(b => b.Label == "Paste");

        vm.DuplicateButtonCommand.Execute(null);

        var copy = Assert.IsType<ButtonEditModel>(vm.Inspecting);
        Assert.Equal("Paste", copy.Label);
        Assert.Equal("ctrl+v", copy.Action.Parameters.Single(p => p.Name == "keys").Value);
        Assert.Equal((0, 0), (copy.Col, copy.Row));
    }

    [Fact]
    public void AddingAPageSwitchesToItAndItStartsEmpty()
    {
        var vm = Open();

        var before = vm.Pages.Count;
        vm.AddPageCommand.Execute(null);

        Assert.Equal(before + 1, vm.Pages.Count);
        Assert.Empty(vm.CurrentPage!.Buttons);
        Assert.Same(vm.CurrentPage, vm.Inspecting);
    }

    [Fact]
    public void SwitchingProfileFollowsItsFirstPage()
    {
        var vm = Open();

        vm.CurrentProfile = vm.Profiles.Single(p => p.Id == "media");

        Assert.Equal("main", vm.CurrentPage!.Id);
        Assert.Equal(3, vm.CurrentProfile!.Columns);
        Assert.False(vm.HasSelection);
    }

    [Fact]
    public void AProblemIsCountedAndWordedForAPerson()
    {
        var vm = Open();

        vm.CurrentProfile!.Columns = 2;

        Assert.True(vm.HasProblems);
        Assert.True(vm.HasErrors);
        Assert.Contains("problem", vm.ProblemSummary, StringComparison.Ordinal);
    }

    [Fact]
    public void TheProblemListIsHiddenUntilThereIsSomethingInIt()
    {
        var vm = Open();

        vm.ShowProblems = true;

        Assert.False(vm.ShowProblems);
    }

    [Fact]
    public void SavingWritesTheNewButtonAndReloadingFindsIt()
    {
        var vm = Open();
        vm.CurrentProfile!.Rows = 4;
        vm.AddButtonAt(1, 3);
        vm.SelectedButton!.Label = "Mute mic";
        vm.SelectedButton.Action.Parameters.Single(p => p.Name == "keys").Value = "ctrl+shift+m";

        vm.Save();

        Assert.False(vm.IsDirty);

        var reloaded = new ConfigLoader(new ConfigPaths(_root), new[] { "hotkey", "launch" }).Load();
        var written = reloaded.FindProfile("default")!.Pages[0].Buttons.Single(b => b.Label == "Mute mic");

        Assert.Equal("ctrl+shift+m", written.Action!.GetString("keys"));
        Assert.Equal(1, written.Col);
        Assert.Equal(3, written.Row);
    }

    [Fact]
    public void DiscardingPutsEverythingBack()
    {
        var vm = Open();
        var before = vm.CurrentPage!.Buttons.Count;
        vm.CurrentProfile!.Rows = 4;
        vm.AddButtonAt(0, 3);

        vm.DiscardCommand.Execute(null);

        Assert.False(vm.IsDirty);
        Assert.Equal(before, vm.CurrentPage!.Buttons.Count);
        Assert.Equal(3, vm.CurrentProfile!.Rows);
    }

    [Fact]
    public void ThePreviewTakesTheShapeOfAScreen()
    {
        var vm = Open();

        Assert.True(vm.PreviewAspect > 0);
    }
}
