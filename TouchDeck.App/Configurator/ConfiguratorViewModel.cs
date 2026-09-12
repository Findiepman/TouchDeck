using System.IO;
using System.Collections.ObjectModel;
using System.Diagnostics;
using Serilog;
using TouchDeck.Core.Actions;
using TouchDeck.Core.Configuration;
using TouchDeck.Platform.Display;

namespace TouchDeck.App.Configurator;

/// <summary>
/// The config center's state. There is one current profile, one current page, and one
/// inspector showing whatever you last touched. Everything else is a command.
/// </summary>
public sealed class ConfiguratorViewModel : ViewModelBase
{
    private readonly ConfigPaths _paths;
    private readonly ConfigLoader _loader;
    private readonly ConfigWriter _writer;
    private readonly ActionRegistry _registry;
    private readonly ILogger _logger;
    private readonly List<Profile> _deleted = new();

    private ProfileEditModel? _currentProfile;
    private PageEditModel? _currentPage;
    private object? _inspecting;
    private bool _isDirty;
    private bool _showProblems;
    private string _status = "";
    private bool _loading;

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

        SaveCommand = new RelayCommand(_ => Save(), _ => IsDirty);
        DiscardCommand = new RelayCommand(_ => Reload(), _ => IsDirty);
        ShowFilesCommand = new RelayCommand(_ => ShowFiles());
        EditSettingsCommand = new RelayCommand(_ => Inspecting = Settings);
        ToggleProblemsCommand = new RelayCommand(_ => ShowProblems = !_showProblems);

        AddProfileCommand = new RelayCommand(_ => AddProfile());
        EditProfileCommand = new RelayCommand(_ => Inspecting = CurrentProfile, _ => CurrentProfile is not null);
        DeleteProfileCommand = new RelayCommand(_ => DeleteProfile(), _ => Profiles.Count > 1);

        AddPageCommand = new RelayCommand(_ => AddPage(), _ => CurrentProfile is not null);
        EditPageCommand = new RelayCommand(_ => Inspecting = CurrentPage, _ => CurrentPage is not null);
        DeletePageCommand = new RelayCommand(_ => DeletePage(), _ => CurrentProfile?.Pages.Count > 1);

        AddThemeCommand = new RelayCommand(_ => AddTheme());
        EditThemeCommand = new RelayCommand(_ => EditTheme(), _ => CurrentProfile?.Theme is { Length: > 0 });

        AddButtonCommand = new RelayCommand(action => AddButtonWith(action as IAction));
        DeleteButtonCommand = new RelayCommand(_ => DeleteButton(), _ => SelectedButton is not null);
        DuplicateButtonCommand = new RelayCommand(_ => DuplicateButton(), _ => SelectedButton is not null);
        ClearSelectionCommand = new RelayCommand(_ => Inspecting = null);

        Reload();
    }

    /// <summary>Raised when the page being shown changes, so the grid can redraw.</summary>
    public event EventHandler? PageChanged;

    /// <summary>Raised after any edit, so the grid can redraw.</summary>
    public event EventHandler? DocumentChanged;

    /// <summary>Raised when a button was just created, so the label box can take focus.</summary>
    public event EventHandler? ButtonAdded;

    /// <summary>Global settings.</summary>
    public SettingsEditModel Settings { get; private set; }

    /// <summary>Every named theme.</summary>
    public ObservableCollection<ThemeEditModel> Themes { get; } = new();

    /// <summary>Every profile.</summary>
    public ObservableCollection<ProfileEditModel> Profiles { get; } = new();

    /// <summary>Everything currently wrong with the configuration.</summary>
    public ObservableCollection<ValidationMessage> Messages { get; } = new();

    /// <summary>Every action type, offered when adding a button.</summary>
    public IReadOnlyList<IAction> AvailableActions =>
        _registry.All.OrderBy(a => a.Title, StringComparer.CurrentCultureIgnoreCase).ToArray();

    /// <summary>Theme names for the profile dropdown, with an entry for the built in look.</summary>
    public IReadOnlyList<string?> ThemeNames =>
        new string?[] { null }.Concat(Themes.Select(t => (string?)t.Name)).ToArray();

    /// <summary>Writes every change to disk.</summary>
    public RelayCommand SaveCommand { get; }

    /// <summary>Throws away unsaved changes.</summary>
    public RelayCommand DiscardCommand { get; }

    /// <summary>Opens the config folder in Explorer.</summary>
    public RelayCommand ShowFilesCommand { get; }

    /// <summary>Shows global settings in the inspector.</summary>
    public RelayCommand EditSettingsCommand { get; }

    /// <summary>Expands or collapses the list of problems.</summary>
    public RelayCommand ToggleProblemsCommand { get; }

    /// <summary>Adds an empty profile.</summary>
    public RelayCommand AddProfileCommand { get; }

    /// <summary>Shows the current profile in the inspector.</summary>
    public RelayCommand EditProfileCommand { get; }

    /// <summary>Removes the current profile, keeping a backup of its file.</summary>
    public RelayCommand DeleteProfileCommand { get; }

    /// <summary>Adds a page to the current profile.</summary>
    public RelayCommand AddPageCommand { get; }

    /// <summary>Shows the current page in the inspector.</summary>
    public RelayCommand EditPageCommand { get; }

    /// <summary>Removes the current page.</summary>
    public RelayCommand DeletePageCommand { get; }

    /// <summary>Adds a theme and points the current profile at it.</summary>
    public RelayCommand AddThemeCommand { get; }

    /// <summary>Shows the current profile's theme in the inspector.</summary>
    public RelayCommand EditThemeCommand { get; }

    /// <summary>Adds a button running the action passed as the parameter.</summary>
    public RelayCommand AddButtonCommand { get; }

    /// <summary>Removes the selected button.</summary>
    public RelayCommand DeleteButtonCommand { get; }

    /// <summary>Copies the selected button into the next free cell.</summary>
    public RelayCommand DuplicateButtonCommand { get; }

    /// <summary>Returns the inspector to the add a button surface.</summary>
    public RelayCommand ClearSelectionCommand { get; }

    /// <summary>The profile being edited.</summary>
    public ProfileEditModel? CurrentProfile
    {
        get => _currentProfile;
        set
        {
            if (!Set(ref _currentProfile, value))
            {
                return;
            }

            Raise(nameof(Pages));
            CurrentPage = value?.Pages.FirstOrDefault();
            Inspecting = null;
        }
    }

    /// <summary>Pages in the current profile.</summary>
    public IReadOnlyList<PageEditModel> Pages =>
        _currentProfile?.Pages.ToArray() ?? Array.Empty<PageEditModel>();

    /// <summary>The page being edited.</summary>
    public PageEditModel? CurrentPage
    {
        get => _currentPage;
        set
        {
            if (Set(ref _currentPage, value))
            {
                Inspecting = null;
                PageChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    /// <summary>Whatever the inspector is showing, or null for the add a button surface.</summary>
    public object? Inspecting
    {
        get => _inspecting;
        set
        {
            if (Set(ref _inspecting, value))
            {
                Raise(nameof(SelectedButton));
                Raise(nameof(HasSelection));
                PageChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    /// <summary>The button being edited, when the inspector is showing one.</summary>
    public ButtonEditModel? SelectedButton => _inspecting as ButtonEditModel;

    /// <summary>True when the inspector is showing something rather than the add surface.</summary>
    public bool HasSelection => _inspecting is not null;

    /// <summary>True when there are changes that have not been written to disk.</summary>
    public bool IsDirty
    {
        get => _isDirty;
        private set
        {
            if (Set(ref _isDirty, value))
            {
                Raise(nameof(Title));
                Raise(nameof(SaveLabel));
            }
        }
    }

    /// <summary>What the window title says.</summary>
    public string Title => IsDirty ? "TouchDeck config center, unsaved" : "TouchDeck config center";

    /// <summary>What the save button says, which is also how you tell whether it worked.</summary>
    public string SaveLabel => IsDirty ? "Save" : "Saved";

    /// <summary>The last thing that happened.</summary>
    public string Status
    {
        get => _status;
        private set => Set(ref _status, value);
    }

    /// <summary>Whether the list of problems is expanded.</summary>
    public bool ShowProblems
    {
        get => _showProblems && Messages.Count > 0;
        set => Set(ref _showProblems, value);
    }

    /// <summary>True when something is wrong somewhere in the configuration.</summary>
    public bool HasProblems => Messages.Count > 0;

    /// <summary>How the problem count reads on the rail.</summary>
    public string ProblemSummary
    {
        get
        {
            var errors = Messages.Count(m => m.Severity == ValidationSeverity.Error);
            var warnings = Messages.Count - errors;

            return (errors, warnings) switch
            {
                (0, 0) => "No problems",
                (0, 1) => "1 warning",
                (0, _) => $"{warnings} warnings",
                (1, 0) => "1 problem",
                (_, 0) => $"{errors} problems",
                _ => $"{errors} problems, {warnings} warnings",
            };
        }
    }

    /// <summary>True when at least one problem stops a file being used at all.</summary>
    public bool HasErrors => Messages.Any(m => m.Severity == ValidationSeverity.Error);

    /// <summary>
    /// The shape of the screen the deck runs on, so the preview is the shape of the real
    /// thing rather than the shape of this window.
    /// </summary>
    public double PreviewAspect
    {
        get
        {
            var monitor = Settings?.SelectedMonitor?.Monitor;
            return monitor is { Width: > 0, Height: > 0 }
                ? (double)monitor.Width / monitor.Height
                : 16.0 / 9.0;
        }
    }

    /// <summary>The theme the current profile draws with, fully resolved.</summary>
    public ResolvedTheme ResolvedTheme => BuildConfiguration().ResolveTheme(CurrentProfile?.Theme);

    /// <summary>Throws away unsaved changes and reads the config folder again.</summary>
    public void Reload()
    {
        _loading = true;

        var loaded = _loader.Load();

        Settings = new SettingsEditModel(loaded.App, MonitorLocator.Enumerate());
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

        _loading = false;
        IsDirty = false;

        _currentProfile = Profiles.FirstOrDefault(p =>
            string.Equals(p.Id, loaded.App.Behaviour.DefaultProfile, StringComparison.OrdinalIgnoreCase))
            ?? Profiles.FirstOrDefault();
        Raise(nameof(CurrentProfile));
        Raise(nameof(Pages));

        _currentPage = _currentProfile?.Pages.FirstOrDefault();
        Raise(nameof(CurrentPage));

        Inspecting = null;
        Raise(nameof(PreviewAspect));
        Validate();

        Status = $"{Profiles.Count} profiles and {Themes.Count} themes, from {_paths.Root}";
        PageChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Writes every change to disk. A running deck picks them up by itself.</summary>
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
                ? "Nothing had changed."
                : $"Saved {written} file{(written == 1 ? string.Empty : "s")}. The deck has it already.";
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
    public void AddButtonAt(int column, int row) => Create(column, row, StartingAction, "New button");

    /// <summary>
    /// What a button does before you have said otherwise. Sending a hotkey is what a deck
    /// button usually is, so pressing an empty square lands you somewhere useful.
    /// </summary>
    private IAction? StartingAction =>
        _registry.TryGet("hotkey", out var hotkey) ? hotkey : AvailableActions.FirstOrDefault();

    /// <summary>Adds a button running a chosen action, in the first free cell.</summary>
    /// <param name="action">The action the new button runs.</param>
    public void AddButtonWith(IAction? action)
    {
        if (FindFreeCell() is not { } free)
        {
            Status = "Every cell is taken. Make the grid bigger, or add a page.";
            return;
        }

        Create(free.Column, free.Row, action, action?.Title ?? "New button");
    }

    /// <summary>Swaps two buttons, which is what dropping one onto another means.</summary>
    /// <param name="moved">The button being dragged.</param>
    /// <param name="other">The button it was dropped on.</param>
    public void Swap(ButtonEditModel moved, ButtonEditModel other)
    {
        (moved.Col, other.Col) = (other.Col, moved.Col);
        (moved.Row, other.Row) = (other.Row, moved.Row);
        Inspecting = moved;
        MarkEdited();
    }

    /// <summary>Marks the document changed and re-runs validation.</summary>
    public void MarkEdited()
    {
        if (_loading)
        {
            return;
        }

        IsDirty = true;
        Raise(nameof(PreviewAspect));
        Validate();
        DocumentChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Rebuilds the configuration from the editors and checks it.</summary>
    public void Validate()
    {
        if (_loading)
        {
            return;
        }

        var messages = new ConfigValidator(_paths, _registry.KnownTypes).Validate(BuildConfiguration());

        Messages.Clear();
        foreach (var message in messages)
        {
            Messages.Add(message);
        }

        Raise(nameof(Messages));
        Raise(nameof(HasProblems));
        Raise(nameof(HasErrors));
        Raise(nameof(ProblemSummary));
        Raise(nameof(ShowProblems));
    }

    /// <summary>Assembles the configuration exactly as it would be saved.</summary>
    public DeckConfiguration BuildConfiguration() => new()
    {
        App = Settings.ToConfig(),
        Themes = Themes.ToDictionary(t => t.Name, t => t.ToConfig(), StringComparer.OrdinalIgnoreCase),
        Profiles = Profiles
            .Select(p => p.ToConfig())
            .Select(p => p with { SourceFile = _writer.FileFor(p) })
            .ToArray(),
    };

    private void Create(int column, int row, IAction? action, string label)
    {
        if (CurrentPage is not { } page)
        {
            return;
        }

        var button = new ButtonEditModel(_registry, new ButtonConfig
        {
            Col = column,
            Row = row,
            Label = label,
        });

        if (action is not null)
        {
            button.Action.Type = action.Type;
        }

        page.Add(button);
        Inspecting = button;
        MarkEdited();
        ButtonAdded?.Invoke(this, EventArgs.Empty);
    }

    private void AddProfile()
    {
        var profile = new ProfileEditModel(_registry, new Profile
        {
            Id = UniqueId("profile", Profiles.Select(p => p.Id)),
            Name = "New profile",
            Theme = Themes.FirstOrDefault()?.Name,
            Grid = new GridConfig { Columns = 5, Rows = 3 },
            Pages = new[] { new Page { Id = "main" } },
        });

        Watch(profile);
        Profiles.Add(profile);
        CurrentProfile = profile;
        Inspecting = profile;
        MarkEdited();
    }

    private void DeleteProfile()
    {
        if (CurrentProfile is not { } profile)
        {
            return;
        }

        _deleted.Add(profile.ToConfig() with { SourceFile = profile.SourceFile });
        Profiles.Remove(profile);
        CurrentProfile = Profiles.FirstOrDefault();
        MarkEdited();
    }

    private void AddPage()
    {
        if (CurrentProfile is not { } profile)
        {
            return;
        }

        var page = new PageEditModel(_registry, new Page
        {
            Id = UniqueId("page", profile.Pages.Select(p => p.Id)),
        });

        profile.Add(page);
        Raise(nameof(Pages));
        CurrentPage = page;
        Inspecting = page;
        MarkEdited();
    }

    private void DeletePage()
    {
        if (CurrentProfile is not { } profile || CurrentPage is not { } page)
        {
            return;
        }

        profile.Pages.Remove(page);
        Raise(nameof(Pages));
        CurrentPage = profile.Pages.FirstOrDefault();
        MarkEdited();
    }

    private void AddTheme()
    {
        var theme = new ThemeEditModel(UniqueId("theme", Themes.Select(t => t.Name)), new Theme());
        Watch(theme);
        Themes.Add(theme);

        if (CurrentProfile is { } profile)
        {
            profile.Theme = theme.Name;
        }

        Raise(nameof(ThemeNames));
        Inspecting = theme;
        MarkEdited();
    }

    private void EditTheme()
    {
        var named = Themes.FirstOrDefault(t =>
            string.Equals(t.Name, CurrentProfile?.Theme, StringComparison.OrdinalIgnoreCase));

        if (named is not null)
        {
            Inspecting = named;
        }
    }

    private void DeleteButton()
    {
        if (SelectedButton is not { } button || CurrentPage is not { } page)
        {
            return;
        }

        page.Buttons.Remove(button);
        Inspecting = null;
        MarkEdited();
    }

    private void DuplicateButton()
    {
        if (SelectedButton is not { } button || CurrentPage is not { } page)
        {
            return;
        }

        if (FindFreeCell() is not { } free)
        {
            Status = "There is no free cell to put a copy in.";
            return;
        }

        var copy = new ButtonEditModel(_registry, button.ToConfig())
        {
            Col = free.Column,
            Row = free.Row,
            ColSpan = 1,
            RowSpan = 1,
        };

        page.Add(copy);
        Inspecting = copy;
        MarkEdited();
    }

    private CellEventArgs? FindFreeCell()
    {
        if (CurrentProfile is not { } profile || CurrentPage is not { } page)
        {
            return null;
        }

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

    private void ShowFiles()
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
