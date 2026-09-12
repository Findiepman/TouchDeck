using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using TouchDeck.Core.Actions;
using TouchDeck.Platform.Windowing;

namespace TouchDeck.App.Configurator;

/// <summary>
/// The config center. It edits the same files the deck reads, so saving here is the same as
/// saving in a text editor: the running panel picks the change up by itself.
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

        _viewModel.PageChanged += (_, _) => RefreshDeck();
        _viewModel.DocumentChanged += (_, _) => RefreshDeck();
        _viewModel.ButtonAdded += (_, _) => FocusLabel();

        Preview.ButtonSelected += (_, button) => _viewModel.Inspecting = button;
        Preview.EmptyCellClicked += (_, cell) => _viewModel.AddButtonAt(cell.Column, cell.Row);
        Preview.ButtonMoved += (_, button) =>
        {
            _viewModel.Inspecting = button;
            _viewModel.MarkEdited();
        };
        Preview.ButtonsSwapped += (_, swap) => _viewModel.Swap(swap.Moved, swap.Other);
        Preview.ButtonDeleted += (_, button) =>
        {
            _viewModel.Inspecting = button;
            _viewModel.DeleteButtonCommand.Execute(null);
        };
        Preview.ButtonDuplicated += (_, button) =>
        {
            _viewModel.Inspecting = button;
            _viewModel.DuplicateButtonCommand.Execute(null);
        };

        InputBindings.Add(new KeyBinding(_viewModel.SaveCommand, Key.S, ModifierKeys.Control));
        InputBindings.Add(new KeyBinding(_viewModel.DuplicateButtonCommand, Key.D, ModifierKeys.Control));

        Loaded += (_, _) => RefreshDeck();
    }

    /// <inheritdoc />
    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        DeckWindowNative.UseDarkTitleBar(new System.Windows.Interop.WindowInteropHelper(this).Handle);
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
            "TouchDeck",
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

    /// <summary>Escape steps back, Delete removes the selected button when no box has focus.</summary>
    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);

        if (Keyboard.FocusedElement is TextBox or HotkeyCaptureBox)
        {
            return;
        }

        switch (e.Key)
        {
            case Key.Escape when _viewModel.HasSelection:
                _viewModel.ClearSelectionCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.Delete when _viewModel.SelectedButton is not null:
                _viewModel.DeleteButtonCommand.Execute(null);
                e.Handled = true;
                break;
        }
    }

    /// <summary>Opens the list of action types for the action the button is showing.</summary>
    private void OnStartPickingAction(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is ActionEditModel action)
        {
            action.Search = string.Empty;
            action.IsPicking = true;
        }
    }

    /// <summary>Takes the action type that was clicked in the list.</summary>
    private void OnPickAction(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: IAction picked } element)
        {
            return;
        }

        if (FindAncestorAction(element) is { } action)
        {
            action.SelectedAction = picked;
        }
    }

    /// <summary>Walks up to the action the clicked row belongs to.</summary>
    private static ActionEditModel? FindAncestorAction(DependencyObject element)
    {
        for (var node = element; node is not null; node = VisualTreeHelper.GetParent(node))
        {
            if (node is FrameworkElement { DataContext: ActionEditModel action })
            {
                return action;
            }
        }

        return null;
    }

    private void RefreshDeck() => Preview.Show(
        _viewModel.CurrentProfile,
        _viewModel.CurrentPage,
        _viewModel.ResolvedTheme,
        _viewModel.SelectedButton);

    /// <summary>
    /// Puts the caret in the label box of a button that was just created, so adding one and
    /// naming it is a single motion.
    /// </summary>
    private void FocusLabel() => Dispatcher.BeginInvoke(
        () =>
        {
            if (FindNamed<TextBox>(Inspector, "LabelBox") is { } box)
            {
                box.Focus();
                box.SelectAll();
            }
        },
        DispatcherPriority.Input);

    private static T? FindNamed<T>(DependencyObject root, string name)
        where T : FrameworkElement
    {
        if (root is T match && match.Name == name)
        {
            return match;
        }

        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            if (FindNamed<T>(VisualTreeHelper.GetChild(root, i), name) is { } found)
            {
                return found;
            }
        }

        return null;
    }
}
