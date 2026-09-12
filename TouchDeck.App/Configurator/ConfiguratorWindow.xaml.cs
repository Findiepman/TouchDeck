using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace TouchDeck.App.Configurator;

/// <summary>
/// The config center. It edits the same JSON files the deck reads, so saving here is the
/// same as saving in a text editor: the running panel picks the change up by itself.
/// </summary>
public partial class ConfiguratorWindow : Window
{
    private readonly ConfiguratorViewModel _viewModel;

    /// <summary>Creates the window.</summary>
    /// <param name="viewModel">The configuration being edited.</param>
    public ConfiguratorWindow(ConfiguratorViewModel viewModel)
    {
        _viewModel = viewModel;
        DataContext = viewModel;

        InitializeComponent();

        _viewModel.SelectionChanged += (_, _) => RefreshPreview();
        _viewModel.DocumentChanged += (_, _) => RefreshPreview();

        Preview.ButtonSelected += (_, button) => _viewModel.Selected = button;
        Preview.EmptyCellClicked += (_, cell) => _viewModel.AddButton(cell.Column, cell.Row);
        Preview.ButtonMoved += (_, _) => _viewModel.MarkEdited();

        Loaded += (_, _) => RefreshPreview();
    }

    /// <summary>Asks before throwing away unsaved changes.</summary>
    protected override void OnClosing(CancelEventArgs e)
    {
        base.OnClosing(e);

        if (!_viewModel.IsDirty)
        {
            return;
        }

        var answer = MessageBox.Show(
            this,
            "Save your changes before closing?",
            "TouchDeck config center",
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Question);

        switch (answer)
        {
            case MessageBoxResult.Yes:
                _viewModel.Save();
                break;
            case MessageBoxResult.Cancel:
                e.Cancel = true;
                break;
        }
    }

    private void OnTreeSelectionChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        switch (e.NewValue)
        {
            case TreeViewItem item when ReferenceEquals(item, SettingsNode):
                _viewModel.Selected = _viewModel.Settings;
                break;
            case TreeViewItem:
                // The Themes and Profiles headers are containers, not things to edit.
                break;
            case { } value:
                _viewModel.Selected = value;
                break;
        }
    }

    private void RefreshPreview()
    {
        Preview.Show(_viewModel.SelectedProfile, _viewModel.SelectedPage, _viewModel.ResolvedTheme);
        Preview.Selected = _viewModel.SelectedButton;
    }
}
