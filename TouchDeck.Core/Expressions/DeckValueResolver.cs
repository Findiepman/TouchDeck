using System.Diagnostics.CodeAnalysis;
using TouchDeck.Core.Variables;

namespace TouchDeck.Core.Expressions;

/// <summary>
/// Resolves the names a condition or a label can refer to. Today that is user variables and
/// environment variables; live providers register themselves here later.
/// </summary>
public sealed class DeckValueResolver : IValueResolver
{
    private readonly IVariableStore _variables;
    private readonly List<IValueResolver> _sources = new();

    /// <summary>Creates a resolver over a variable store.</summary>
    /// <param name="variables">Where <c>var.</c> names come from.</param>
    public DeckValueResolver(IVariableStore variables)
    {
        _variables = variables;
    }

    /// <summary>Adds another place to look, tried in the order added.</summary>
    /// <param name="source">The extra resolver.</param>
    public void Add(IValueResolver source) => _sources.Add(source);

    /// <inheritdoc />
    public bool TryResolve(string reference, [NotNullWhen(true)] out string? value)
    {
        value = null;

        if (string.IsNullOrWhiteSpace(reference))
        {
            return false;
        }

        var dot = reference.IndexOf('.');
        var prefix = dot < 0 ? reference : reference[..dot];
        var rest = dot < 0 ? "" : reference[(dot + 1)..];

        if (rest.Length > 0)
        {
            if (prefix.Equals("var", StringComparison.OrdinalIgnoreCase))
            {
                return _variables.TryGet(rest, out value);
            }

            if (prefix.Equals("env", StringComparison.OrdinalIgnoreCase))
            {
                value = Environment.GetEnvironmentVariable(rest);
                return value is not null;
            }
        }

        foreach (var source in _sources)
        {
            if (source.TryResolve(reference, out value))
            {
                return true;
            }
        }

        return false;
    }
}
