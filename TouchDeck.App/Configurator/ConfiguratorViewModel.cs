using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using Serilog;
using TouchDeck.Core.Actions;
using TouchDeck.Core.Configuration;
using TouchDeck.Platform.Display;

namespace TouchDeck.App.Configurator;

/// <summary>
/// The config center's state: the whole configuration in editable form, what is selected,
/// what is wrong with it, and the commands that change it.
/// </summary>
public sealed class ConfiguratorViewModel : ViewModelBase
{
    private readonly ConfigPaths _paths;
    private readonly ConfigLoader _loader;
    private readonly ConfigWriter _writer;
    private readonly ActionRegistry _registry;
    private readonly ILogger _logger;
    private readonly List<Profile> _deleted = new();

    private object? _selected;
    private bool _isDirty;
    private string _status = "";
    private bool _suppressValidation;

    /// <summary>Creates the view model and loads the current configuration.</summary>
    /// <param name="paths">Where the config lives.</param>
    /// <param name="registry">Where action types come from.</param>
    /// <param name="logger">Where saves and failures are recorded.</param>
    public ConfiguratorViewModel(ConfigPaths paths, ActionRegistry registry, ILogger logger)
    {
        _paths = paths;
        _registry = registry;
        _logger = logger.ForContext<ConfiguratorViewModel>();
        _loader = new ConfigLoader(paths, registry.KnownTypes);
        _writer = new ConfigWriter(paths);

        Settings = null!;
        Reload();

        SaveCommand = new RelayCommand(_ => Save(), _ => IsDirty);
        ReloadCommand = new RelayCommand(_ => Reload());
        OpenFolderCommand = new RelayCommand(_ => OpenConfigFolder());
        AddProfileCommand = new RelayCommand(_ => AddProfile());
        DeleteProfileCommand = new RelayCommand(_ => DeleteSelectedProfile(), _ => SelectedProfile is not null && Profiles.Count > 1);
        AddPageCommand = new RelayCommand(_ => AddPage(), _ => SelectedProfile is not null);
        DeletePageCommand = new RelayCommand(_ => DeleteSelectedPage(), _ => SelectedPage is not null && SelectedProfile?.Pages.Count > 1);
        AddThemeCommand = new RelayCommand(_ => AddTheme());
        DeleteThemeCommand = new RelayCommand(_ => DeleteSelectedTheme(), _ => Selected is ThemeEditModel && Themes.Count > 1);
        DeleteButtonCommand = new RelayCommand(_ => DeleteSelectedButton(), _ => SelectedButton is not null);
        DuplicateButtonCommand = new RelayCommand(_ => DuplicateSelectedButton(), _ => SelectedButton is not null);
    }

    /// <summary>Raised when the selection changes, so the view can redraw the grid.</summary>
    public event EventHandler? SelectionChanged;

    /// <summary>Raised after any edit, so the view can redraw the preview.</summary>
    public event EventHandler? DocumentChanged;

    /// <summary>Global settings.</summary>
    public SettingsEditModel Settings { get; private set; }

    /// <summary>Every named theme.</summary>
    public ObservableCollection<ThemeEditModel> Themes { get; } = new();

    /// <summary>Every profile.</summary>
    public ObservableCollection<ProfileEditModel> Profiles { get; } = new();

    /// <summary>Everything currently wrong with the configuration.</summary>
    public ObservableCollection<ValidationMessage> Messages { get; } = new();

    /// <summary>Theme names offered in the profile dropdown, with an entry for "built in defaults".</summary>
    public IReadOnlyList<string?> ThemeNames =>
        new string?[] { null }.Concat(Themes.Select(t => (string?)t.Name)).ToArray();

    /// <summary>Saves every change to disk.</summary>
    public RelayCommand SaveCommand { get; }

    /// <summary>Throws away unsaved changes and reads the files again.</summary>
    public RelayCommand ReloadCommand { get; }

    /// <summary>Opens the config folder in Explorer.</summary>
    public RelayCommand OpenFolderCommand { get; }

    /// <summary>Adds an empty profile.</summary>
    public RelayCommand AddProfileCommand { get; }

    /// <summary>Removes the selected profile, keeping a backup of its file.</summary>
    public RelayCommand DeleteProfileCommand { get; }

    /// <summary>Adds a page to the selected profile.</summary>
    public RelayCommand AddPageCommand { get; }

    /// <summary>Removes the selected page.</summary>
    public RelayCommand DeletePageCommand { get; }

    /// <summary>Adds a theme.</summary>
    public RelayCommand AddThemeCommand { get; }

    /// <summary>Removes the selected theme.</summary>
    public RelayCommand DeleteThemeCommand { get; }

    /// <summary>Removes the selected button.</summary>
    public RelayCommand DeleteButtonCommand { get; }

    /// <summary>Copies the selected button into the next free cell.</summary>
    public RelayCommand DuplicateButtonCommand { get; }

    /// <summary>Whatever is selected in the tree or on the grid.</summary>
    public object? Selected
    {
        get => _selected;
        set
        {
            if (Set(ref _selected, value))
            {
                Raise(nameof(SelectedProfile));
                Raise(nameof(SelectedPage));
                Raise(nameof(SelectedButton));
                SelectionChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    /// <summary>The profile the selection belongs to.</summary>
    public ProfileEditModel? SelectedProfile => _selected switch
    {
        ProfileEditModel profile => profile,
        PageEditModel page => Profiles.FirstOrDefault(p => p.Pages.Contains(page)),
        ButtonEditModel button => Profiles.FirstOrDefault(p => p.Pages.Any(pg => pg.Buttons.Contains(button))),
        _ => null,
    };

    /// <summary>The page the selection belongs to, or the first page of a selected profile.</summary>
    public PageEditModel? SelectedPage => _selected switch
    {
        PageEditModel page => page,
        ButtonEditModel button => Profiles.SelectMany(p => p.Pages).FirstOrDefault(pg => pg.Buttons.Contains(button)),
        ProfileEditModel profile => profile.Pages.FirstOrDefault(),
        _ => null,
    };

    /// <summary>The selected button, when one is selected.</summary>
    public ButtonEditModel? SelectedButton => _selected as ButtonEditModel;

    /// <summary>True when there are changes that have not been written to disk.</summary>
    public bool IsDirty
    {
        get => _isDirty;
        private set
        {
            if (Set(ref _isDirty, value))
            {
                Raise(nameof(Title));
            }
        }
    }

    /// <summary>What the window title says.</summary>
    public string Title => IsDirty ? "TouchDeck config center *" : "TouchDeck config center";

    /// <summary>The last thing that happened, shown in the status bar.</summary>
    public string Status
    {
        get => _status;
        private set => Set(ref _status, value);
    }

    /// <summary>Number of errors, which are the things that stop a file being used at all.</summary>
    public int ErrorCount => Messages.Count(m => m.Severity == ValidationSeverity.Error);

    /// <summary>The theme the selected profile draws with, fully resolved.</summary>
    public ResolvedTheme ResolvedTheme => BuildConfiguration().ResolveTheme(SelectedProfile?.Theme);

    /// <summary>Throws away unsaved changes and reads the config folder again.</summary>
    public void Reload()
    {
        _suppressValidation = true;

        var loaded = _loader.Load();
        var monitors = MonitorLocator.Enumerate();

        Settings = new SettingsEditModel(loaded.App, monitors);
        Watch(Settings);
        Raise(nameof(Settings));

        Themes.Clear();
        foreach (var (name, theme) in loaded.Themes.OrderBy(t => t.Key, StringComparer.CurrentCultureIgnoreCase))
        {
            var model = new ThemeEditModel(name, theme);
            Watch(model);
            Themes.Add(model);
        }

        Profiles.Clear();
        foreach (var profile in loaded.Profiles)
        {
            var model = new ProfileEditModel(_registry, profile);
            Watch(model);
            Profiles.Add(model);
        }

        _deleted.Clear();
        Raise(nameof(ThemeNames));

        _suppressValidation = false;
        IsDirty = false;
        Selected = Profiles.FirstOrDefault()?.Pages.FirstOrDefault() ?? (object?)Settings;
        Validate();
        Status = $"Loaded {Profiles.Count} profiles and {Themes.Count} themes from {_paths.Root}";
    }

    /// <summary>Writes every change to disk. The running deck picks them up by itself.</summary>
    public void Save()
    {
        try
        {
            var written = 0;

            foreach (var profile in _deleted)
            {
                _writer.DeleteProfile(profile);
                written++;
            }

            _deleted.Clear();

            if (_writer.SaveAppConfig(Settings.ToConfig()))
            {
                written++;
            }

            if (_writer.SaveThemes(Themes.ToDictionary(t => t.Name, t => t.ToConfig(), StringComparer.OrdinalIgnoreCase)))
            {
                written++;
            }

            foreach (var profile in Profiles)
            {
                if (_writer.SaveProfile(profile.ToConfig()))
                {
                    written++;
                }
            }

            IsDirty = false;
            Status = written == 0
                ? "Nothing had changed, so no file was written."
                : $"Saved. {written} file(s) written, previous versions kept as .bak.";
            _logger.Information("Config center saved {Count} files.", written);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Status = $"Could not save: {ex.Message}";
            _logger.Error(ex, "Config center could not save.");
        }
    }

    /// <summary>Adds a button at a given cell and selects it.</summary>
    /// <param name="column">Zero based column.</param>
    /// <param name="row">Zero based row.</param>
    public void AddButton(int column, int row)
    {
        if (SelectedPage is not { } page)
        {
            return;
        }

        var button = new ButtonEditModel(_registry, new ButtonConfig
        {
            Col = column,
            Row = row,
            Label = "New button",
        });

        page.Add(button);
        Selected = button;
        MarkEdited();
    }

    /// <summary>Marks the document changed and re-runs validation.</summary>
    public void MarkEdited()
    {
        if (_suppressValidation)
        {
            return;
        }

        IsDirty = true;
        Validate();
        DocumentChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Rebuilds the configuration from the editors and checks it.</summary>
    public void Validate()
    {
        if (_suppressValidation)
        {
            return;
        }

        var configuration = BuildConfiguration();
        var messages = new ConfigValidator(_paths, _registry.KnownTypes).Validate(configuration);

        Messages.Clear();
        foreach (var message in messages)
        {
            Messages.Add(message);
        }

        Raise(nameof(ErrorCount));
        Raise(nameof(Messages));
    }

    /// <summary>Assembles the configuration exactly as it would be saved.</summary>
    public DeckConfiguration BuildConfiguration() => new()
    {
        App = Settings.ToConfig(),
        Themes = Themes.ToDictionary(t => t.Name, t => t.ToConfig(), StringComparer.OrdinalIgnoreCase),
        Profiles = Profiles.Select(p => p.ToConfig() with { SourceFile = _writer.FileFor(p.ToConfig()) }).ToArray(),
    };

    private void AddProfile()
    {
        var id = UniqueId("profile", Profiles.Select(p => p.Id));

        var profile = new ProfileEditModel(_registry, new Profile
        {
            Id = id,
            Name = "New profile",
            Theme = Themes.FirstOrDefault()?.Name,
            Grid = new GridConfig { Columns = 5, Rows = 3 },
            Pages = new[] { new Page { Id = "main" } },
        });

        Watch(profile);
        Profiles.Add(profile);
        Selected = profile;
        MarkEdited();
    }

    private void DeleteSelectedProfile()
    {
        if (SelectedProfile is not { } profile)
        {
            return;
        }

        _deleted.Add(profile.ToConfig() with { SourceFile = profile.SourceFile });
        Profiles.Remove(profile);
        Selected = Profiles.FirstOrDefault();
        MarkEdited();
    }

    private void AddPage()
    {
        if (SelectedProfile is not { } profile)
        {
            return;
        }

        var page = new PageEditModel(_registry, new Page
        {
            Id = UniqueId("page", profile.Pages.Select(p => p.Id)),
        });

        profile.Add(page);
        Selected = page;
        MarkEdited();
    }

    private void DeleteSelectedPage()
    {
        if (SelectedProfile is not { } profile || SelectedPage is not { } page)
        {
            return;
        }

        profile.Pages.Remove(page);
        Selected = profile.Pages.FirstOrDefault() ?? (object)profile;
        MarkEdited();
    }

    private void AddTheme()
    {
        var theme = new ThemeEditModel(UniqueId("theme", Themes.Select(t => t.Name)), new Theme());
        Watch(theme);
        Themes.Add(theme);
        Selected = theme;
        Raise(nameof(ThemeNames));
        MarkEdited();
    }

    private void DeleteSelectedTheme()
    {
        if (Selected is not ThemeEditModel theme)
        {
            return;
        }

        Themes.Remove(theme);
        Selected = Themes.FirstOrDefault();
        Raise(nameof(ThemeNames));
        MarkEdited();
    }

    private void DeleteSelectedButton()
    {
        if (SelectedButton is not { } button || SelectedPage is not { } page)
        {
            return;
        }

        page.Buttons.Remove(button);
        Selected = page;
        MarkEdited();
    }

    private void DuplicateSelectedButton()
    {
        if (SelectedButton is not { } button || SelectedPage is not { } page || SelectedProfile is not { } profile)
        {
            return;
        }

        var copy = new ButtonEditModel(_registry, button.ToConfig());

        if (FindFreeCell(profile, page) is not { } free)
        {
            Status = "There is no free cell to put a copy in.";
            return;
        }

        copy.Col = free.Column;
        copy.Row = free.Row;
        copy.ColSpan = 1;
        copy.RowSpan = 1;

        page.Add(copy);
        Selected = copy;
        MarkEdited();
    }

    private static CellEventArgs? FindFreeCell(ProfileEditModel profile, PageEditModel page)
    {
        var taken = new HashSet<(int, int)>();
        foreach (var button in page.Buttons)
        {
            for (var c = button.Col; c < button.Col + button.ColSpan; c++)
            {
                for (var r = button.Row; r < button.Row + button.RowSpan; r++)
                {
                    taken.Add((c, r));
                }
            }
        }

        for (var row = 0; row < profile.Rows; row++)
        {
            for (var column = 0; column < profile.Columns; column++)
            {
                if (!taken.Contains((column, row)))
                {
                    return new CellEventArgs(column, row);
                }
            }
        }

        return null;
    }

    private void OpenConfigFolder()
    {
        try
        {
            Directory.CreateDirectory(_paths.Root);
            Process.Start(new ProcessStartInfo(_paths.Root) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Status = $"Could not open the folder: {ex.Message}";
        }
    }

    /// <summary>Treats any edit inside a model as an edit to the document.</summary>
    private void Watch(ObservableObject model) => model.Edited += (_, _) => MarkEdited();

    private static string UniqueId(string prefix, IEnumerable<string> existing)
    {
        var taken = new HashSet<string>(existing, StringComparer.OrdinalIgnoreCase);

        for (var i = 1; ; i++)
        {
            var candidate = $"{prefix}{i}";
            if (taken.Add(candidate))
            {
                return candidate;
            }
        }
    }
}
