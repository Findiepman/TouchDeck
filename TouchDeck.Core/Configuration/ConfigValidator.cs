namespace TouchDeck.Core.Configuration;

/// <summary>
/// Checks a loaded configuration for the mistakes a hand editor actually makes: duplicate
/// ids, buttons off the edge of the grid, references to things that do not exist.
/// </summary>
public sealed class ConfigValidator
{
    private readonly ConfigPaths _paths;
    private readonly IReadOnlyCollection<string> _knownActionTypes;

    /// <summary>Creates a validator.</summary>
    /// <param name="paths">Used to describe files relative to the config root.</param>
    /// <param name="knownActionTypes">Executable action types, or empty to skip that check.</param>
    public ConfigValidator(ConfigPaths paths, IReadOnlyCollection<string>? knownActionTypes = null)
    {
        _paths = paths;
        _knownActionTypes = knownActionTypes ?? Array.Empty<string>();
    }

    /// <summary>Returns everything wrong with <paramref name="configuration"/>.</summary>
    public IReadOnlyList<ValidationMessage> Validate(DeckConfiguration configuration)
    {
        var messages = new List<ValidationMessage>();

        ValidateApp(configuration, messages);

        var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var profile in configuration.Profiles)
        {
            var file = _paths.Describe(profile.SourceFile);

            if (!seenIds.Add(profile.Id))
            {
                messages.Add(ValidationMessage.Error(
                    file,
                    "id",
                    $"Another profile already uses the id \"{profile.Id}\". Ids must be unique."));
            }

            ValidateProfile(configuration, profile, file, messages);
        }

        return messages;
    }

    private void ValidateApp(DeckConfiguration configuration, List<ValidationMessage> messages)
    {
        var file = _paths.Describe(_paths.ConfigFile);
        var behaviour = configuration.App.Behaviour;

        if (configuration.Profiles.Count > 0 && configuration.FindProfile(behaviour.DefaultProfile) is null)
        {
            messages.Add(ValidationMessage.Warning(
                file,
                "behaviour.defaultProfile",
                $"No profile has the id \"{behaviour.DefaultProfile}\". " +
                $"Falling back to \"{configuration.Profiles[0].Id}\"."));
        }

        if (behaviour.DoubleTapMs > behaviour.LongPressMs)
        {
            messages.Add(ValidationMessage.Warning(
                file,
                "behaviour.doubleTapMs",
                "doubleTapMs is longer than longPressMs, so long presses will win over double taps."));
        }

        if (configuration.App.Display is { Select: DisplaySelect.ByName or DisplaySelect.ByIndex or DisplaySelect.ByResolution, Value: null or "" })
        {
            messages.Add(ValidationMessage.Warning(
                file,
                "display.value",
                $"display.select is \"{configuration.App.Display.Select}\" but display.value is empty."));
        }
    }

    private void ValidateProfile(
        DeckConfiguration configuration,
        Profile profile,
        string file,
        List<ValidationMessage> messages)
    {
        if (profile.Theme is { Length: > 0 } themeName && !configuration.Themes.ContainsKey(themeName))
        {
            messages.Add(ValidationMessage.Warning(
                file,
                "theme",
                $"No theme named \"{themeName}\" exists in themes.json. Built in defaults are in use."));
        }

        if (profile.Grid.Columns < 1 || profile.Grid.Rows < 1)
        {
            messages.Add(ValidationMessage.Error(
                file,
                "grid",
                $"The grid must have at least one column and one row, not {profile.Grid.Columns} by {profile.Grid.Rows}."));
            return;
        }

        if (profile.Pages.Count == 0)
        {
            messages.Add(ValidationMessage.Error(file, "pages", "A profile needs at least one page."));
            return;
        }

        var pageIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var pageIndex = 0; pageIndex < profile.Pages.Count; pageIndex++)
        {
            var page = profile.Pages[pageIndex];
            var pagePath = $"pages[{pageIndex}]";

            if (string.IsNullOrWhiteSpace(page.Id))
            {
                messages.Add(ValidationMessage.Error(file, $"{pagePath}.id", "A page needs an id."));
            }
            else if (!pageIds.Add(page.Id))
            {
                messages.Add(ValidationMessage.Error(
                    file,
                    $"{pagePath}.id",
                    $"Another page already uses the id \"{page.Id}\"."));
            }

            ValidatePage(profile, page, pagePath, file, messages);
        }
    }

    private void ValidatePage(
        Profile profile,
        Page page,
        string pagePath,
        string file,
        List<ValidationMessage> messages)
    {
        var occupied = new Dictionary<(int Col, int Row), string>();

        var all = page.Buttons
            .Select((b, i) => (Button: b, Path: $"{pagePath}.buttons[{i}]"))
            .ToList();

        if (page.BackButton is not null)
        {
            all.Add((page.BackButton, $"{pagePath}.backButton"));
        }

        foreach (var (button, path) in all)
        {
            ValidateButton(profile, button, path, file, occupied, messages);
        }
    }

    private void ValidateButton(
        Profile profile,
        ButtonConfig button,
        string path,
        string file,
        Dictionary<(int Col, int Row), string> occupied,
        List<ValidationMessage> messages)
    {
        var grid = profile.Grid;

        if (button.ColSpan is < 1 || button.RowSpan is < 1)
        {
            messages.Add(ValidationMessage.Warning(
                file,
                path,
                "colSpan and rowSpan must be at least 1. Treating them as 1."));
        }

        var colSpan = button.SpanColumns;
        var rowSpan = button.SpanRows;

        if (button.Col < 0 || button.Row < 0 || button.Col + colSpan > grid.Columns || button.Row + rowSpan > grid.Rows)
        {
            messages.Add(ValidationMessage.Error(
                file,
                path,
                $"The button at column {button.Col}, row {button.Row} does not fit in a " +
                $"{grid.Columns} by {grid.Rows} grid."));
            return;
        }

        for (var c = button.Col; c < button.Col + colSpan; c++)
        {
            for (var r = button.Row; r < button.Row + rowSpan; r++)
            {
                if (occupied.TryGetValue((c, r), out var other))
                {
                    messages.Add(ValidationMessage.Warning(
                        file,
                        path,
                        $"Cell {c},{r} is already used by {other}. The last button drawn wins."));
                }
                else
                {
                    occupied[(c, r)] = path;
                }
            }
        }

        ValidateIcon(button.Icon, $"{path}.icon", file, messages);

        var actions = new (ActionConfig? Action, string Slot)[]
        {
            (button.Action, "action"),
            (button.ReleaseAction, "releaseAction"),
            (button.LongPressAction, "longPressAction"),
            (button.DoubleTapAction, "doubleTapAction"),
        };

        if (actions.All(a => a.Action is null))
        {
            messages.Add(ValidationMessage.Warning(
                file,
                path,
                "This button has no action, so pressing it does nothing."));
        }

        if (_knownActionTypes.Count == 0)
        {
            return;
        }

        foreach (var (action, slot) in actions)
        {
            if (action is not null && !_knownActionTypes.Contains(action.Type))
            {
                messages.Add(ValidationMessage.Warning(
                    file,
                    $"{path}.{slot}",
                    $"There is no action type called \"{action.Type}\"."));
            }
        }
    }

    /// <summary>
    /// Checks an icon can actually be drawn. Everything here is a warning: a button whose
    /// icon is missing still works, it just shows its label instead.
    /// </summary>
    private void ValidateIcon(
        IconConfig? icon,
        string path,
        string file,
        List<ValidationMessage> messages)
    {
        if (icon is null || icon.Type == IconKind.None)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(icon.Value))
        {
            messages.Add(ValidationMessage.Warning(
                file,
                $"{path}.value",
                $"An icon of type \"{icon.Type.ToString().ToLowerInvariant()}\" needs a value."));
            return;
        }

        if (icon.Type != IconKind.File || IconFile.IsInterpolated(icon.Value))
        {
            return;
        }

        if (IconFile.Resolve(_paths.IconsDirectory, icon.Value) is not { } resolved)
        {
            messages.Add(ValidationMessage.Warning(
                file,
                $"{path}.value",
                $"\"{icon.Value}\" is not a usable path."));
            return;
        }

        if (IconFile.IsSvg(resolved))
        {
            messages.Add(ValidationMessage.Warning(
                file,
                $"{path}.value",
                "Svg icons are not supported yet. Use a png, or an icon of type \"glyph\"."));
            return;
        }

        if (!IconFile.IsSupported(resolved))
        {
            messages.Add(ValidationMessage.Warning(
                file,
                $"{path}.value",
                $"\"{icon.Value}\" is not an image TouchDeck can read. " +
                $"Supported: {string.Join(", ", IconFile.SupportedExtensions)}."));
            return;
        }

        if (!File.Exists(resolved))
        {
            messages.Add(ValidationMessage.Warning(
                file,
                $"{path}.value",
                $"There is no icon at \"{_paths.Describe(resolved)}\"."));
        }
    }
}
