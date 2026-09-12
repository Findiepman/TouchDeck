namespace TouchDeck.Core.Configuration;

/// <summary>
/// Everything the deck needs to run: global settings, themes, profiles and whatever was
/// wrong with the files they came from.
/// </summary>
public sealed record DeckConfiguration
{
    /// <summary>Depth limit for <see cref="Theme.Inherits"/> chains, which also breaks cycles.</summary>
    private const int MaxThemeInheritanceDepth = 16;

    public AppConfig App { get; init; } = new();

    public IReadOnlyDictionary<string, Theme> Themes { get; init; } =
        new Dictionary<string, Theme>(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<Profile> Profiles { get; init; } = Array.Empty<Profile>();

    /// <summary>Problems found while loading. Rendered as an overlay on the panel.</summary>
    public IReadOnlyList<ValidationMessage> Messages { get; init; } = Array.Empty<ValidationMessage>();

    /// <summary>True when at least one file could not be used.</summary>
    public bool HasErrors => Messages.Any(m => m.Severity == ValidationSeverity.Error);

    /// <summary>Finds a profile by id, case insensitively.</summary>
    public Profile? FindProfile(string? id) =>
        string.IsNullOrWhiteSpace(id)
            ? null
            : Profiles.FirstOrDefault(p => string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase));

    /// <summary>The profile to show at startup, or null when no profile loaded at all.</summary>
    public Profile? StartupProfile => FindProfile(App.Behaviour.DefaultProfile) ?? Profiles.FirstOrDefault();

    /// <summary>
    /// Resolves a theme name into a fully populated theme, following <see cref="Theme.Inherits"/>
    /// and falling back to the built in defaults when the name is unknown.
    /// </summary>
    public ResolvedTheme ResolveTheme(string? name)
    {
        var chain = new List<Theme>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var current = name;

        while (!string.IsNullOrWhiteSpace(current)
               && chain.Count < MaxThemeInheritanceDepth
               && seen.Add(current)
               && Themes.TryGetValue(current, out var theme))
        {
            chain.Add(theme);
            current = theme.Inherits;
        }

        // chain is most derived first, so merge from the far end back down.
        var merged = Theme.Defaults;
        for (var i = chain.Count - 1; i >= 0; i--)
        {
            merged = chain[i].MergedOnto(merged);
        }

        return merged.Resolve();
    }
}
