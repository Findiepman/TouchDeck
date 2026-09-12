using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace TouchDeck.App.Configurator;

/// <summary>
/// The small amount of change notification the config center needs. The deck panel itself
/// does not bind to anything, so this lives with the editor rather than in the core.
/// </summary>
public abstract class ObservableObject : INotifyPropertyChanged
{
    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Raised whenever any property on this object changes.</summary>
    public event EventHandler? Edited;

    /// <summary>Assigns a field and raises change notifications when the value actually differs.</summary>
    /// <typeparam name="T">Type of the value.</typeparam>
    /// <param name="field">The backing field.</param>
    /// <param name="value">The new value.</param>
    /// <param name="name">Filled in by the compiler.</param>
    /// <returns>True when the value changed.</returns>
    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        Raise(name);
        return true;
    }

    /// <summary>Raises change notification for a property, and marks the document edited.</summary>
    /// <param name="name">The property name.</param>
    protected void Raise([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        Edited?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Forwards another object's edits as this object's own.</summary>
    /// <param name="child">The nested object to listen to.</param>
    protected void Adopt(ObservableObject? child)
    {
        if (child is not null)
        {
            child.Edited += (_, _) => Edited?.Invoke(this, EventArgs.Empty);
        }
    }
}

/// <summary>
/// Change notification without the edited signal. The config center's own state, such as
/// what is selected, must not mark the document as changed.
/// </summary>
public abstract class ViewModelBase : INotifyPropertyChanged
{
    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Assigns a field and raises change notification when the value differs.</summary>
    /// <typeparam name="T">Type of the value.</typeparam>
    /// <param name="field">The backing field.</param>
    /// <param name="value">The new value.</param>
    /// <param name="name">Filled in by the compiler.</param>
    /// <returns>True when the value changed.</returns>
    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        Raise(name);
        return true;
    }

    /// <summary>Raises change notification for a property.</summary>
    /// <param name="name">The property name.</param>
    protected void Raise([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>A command backed by a delegate.</summary>
public sealed class RelayCommand : ICommand
{
    private readonly Action<object?> _execute;
    private readonly Func<object?, bool>? _canExecute;

    /// <summary>Creates a command.</summary>
    /// <param name="execute">What the command does.</param>
    /// <param name="canExecute">Whether it is currently available.</param>
    public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    /// <inheritdoc />
    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    /// <inheritdoc />
    public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;

    /// <inheritdoc />
    public void Execute(object? parameter) => _execute(parameter);
}
