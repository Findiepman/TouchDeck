using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace TouchDeck.Core.Variables;

/// <summary>How long a variable lives.</summary>
public enum VariableScope
{
    /// <summary>Forgotten when the deck closes.</summary>
    Session,

    /// <summary>Written to disk and still there next time.</summary>
    Persistent,
}

/// <summary>Values the user sets from buttons and reads back in labels and expressions.</summary>
public interface IVariableStore
{
    /// <summary>Raised whenever a value changes, so anything showing it can redraw.</summary>
    event EventHandler? Changed;

    /// <summary>Reads a variable.</summary>
    /// <param name="name">Name without the <c>var.</c> prefix.</param>
    /// <param name="value">The value when the variable exists.</param>
    bool TryGet(string name, [NotNullWhen(true)] out string? value);

    /// <summary>Sets a variable, or removes it when the value is null.</summary>
    /// <param name="name">Name without the <c>var.</c> prefix.</param>
    /// <param name="value">The new value, or null to remove it.</param>
    /// <param name="scope">Whether it survives a restart.</param>
    void Set(string name, string? value, VariableScope scope);

    /// <summary>Flips a variable between true and false, treating anything unset as false.</summary>
    /// <param name="name">Name without the <c>var.</c> prefix.</param>
    /// <returns>The value it ended up with.</returns>
    bool Toggle(string name);

    /// <summary>Every variable currently set.</summary>
    IReadOnlyDictionary<string, string> Snapshot();
}

/// <summary>
/// Keeps variables in memory, and writes the persistent ones to a small file beside the
/// config so a toggle survives a restart.
/// </summary>
public sealed class VariableStore : IVariableStore
{
    private readonly string? _path;
    private readonly Dictionary<string, string> _values = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _persistent = new(StringComparer.OrdinalIgnoreCase);
    private readonly Lock _gate = new();

    /// <summary>Creates a store.</summary>
    /// <param name="path">Where persistent variables are kept, or null to keep none.</param>
    public VariableStore(string? path = null)
    {
        _path = path;
        Load();
    }

    /// <inheritdoc />
    public event EventHandler? Changed;

    /// <inheritdoc />
    public bool TryGet(string name, [NotNullWhen(true)] out string? value)
    {
        lock (_gate)
        {
            return _values.TryGetValue(name, out value);
        }
    }

    /// <inheritdoc />
    public void Set(string name, string? value, VariableScope scope)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        lock (_gate)
        {
            if (value is null)
            {
                _values.Remove(name);
                _persistent.Remove(name);
            }
            else
            {
                _values[name] = value;

                if (scope == VariableScope.Persistent)
                {
                    _persistent.Add(name);
                }
                else
                {
                    _persistent.Remove(name);
                }
            }

            Save();
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    public bool Toggle(string name)
    {
        bool next;

        lock (_gate)
        {
            var current = _values.TryGetValue(name, out var value) && IsTrue(value);
            next = !current;
            _values[name] = next ? "true" : "false";
            Save();
        }

        Changed?.Invoke(this, EventArgs.Empty);
        return next;
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, string> Snapshot()
    {
        lock (_gate)
        {
            return new Dictionary<string, string>(_values, StringComparer.OrdinalIgnoreCase);
        }
    }

    /// <summary>Whether a written value counts as true.</summary>
    /// <param name="value">The value to judge.</param>
    public static bool IsTrue(string? value) =>
        value is not null
        && (value.Equals("true", StringComparison.OrdinalIgnoreCase)
            || value.Equals("on", StringComparison.OrdinalIgnoreCase)
            || value.Equals("yes", StringComparison.OrdinalIgnoreCase)
            || value == "1");

    private void Load()
    {
        if (_path is null || !File.Exists(_path))
        {
            return;
        }

        try
        {
            var json = File.ReadAllText(_path);
            var stored = JsonSerializer.Deserialize<Dictionary<string, string>>(json);

            if (stored is null)
            {
                return;
            }

            foreach (var (name, value) in stored)
            {
                _values[name] = value;
                _persistent.Add(name);
            }
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            // A corrupt variables file is not worth taking the deck down for.
        }
    }

    private void Save()
    {
        if (_path is null)
        {
            return;
        }

        try
        {
            var persistent = _persistent.ToDictionary(name => name, name => _values[name]);
            Directory.CreateDirectory(Path.GetDirectoryName(_path) ?? ".");
            File.WriteAllText(_path, JsonSerializer.Serialize(persistent, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Same again: losing a remembered toggle is better than losing the deck.
        }
    }
}
