using Serilog;
using TouchDeck.Core.Actions;
using TouchDeck.Core.Configuration;

namespace TouchDeck.App.ViewModels;

/// <summary>
/// What the panel is currently showing. The view renders from this and hands presses back;
/// it never decides what a press means.
/// </summary>
public sealed class DeckViewModel
{
    private readonly ActionDispatcher _dispatcher;
    private readonly ILogger _logger;

    /// <summary>Creates the view model.</summary>
    /// <param name="dispatcher">Where presses are sent.</param>
    /// <param name="logger">Where surface changes are recorded.</param>
    public DeckViewModel(ActionDispatcher dispatcher, ILogger logger)
    {
        _dispatcher = dispatcher;
        _logger = logger.ForContext<DeckViewModel>();
        Theme = Core.Configuration.Theme.Defaults.Resolve();
    }

    /// <summary>Raised when the visible profile, page or theme changed and the view must redraw.</summary>
    public event EventHandler? SurfaceChanged;

    /// <summary>The configuration currently in force.</summary>
    public DeckConfiguration Configuration { get; private set; } = new();

    /// <summary>The profile being shown, or null when no profile could be loaded.</summary>
    public Profile? Profile { get; private set; }

    /// <summary>The page being shown, or null when the profile has no pages.</summary>
    public Page? Page { get; private set; }

    /// <summary>The fully resolved theme for the current profile.</summary>
    public ResolvedTheme Theme { get; private set; }

    /// <summary>Buttons on the current page.</summary>
    public IReadOnlyList<ButtonConfig> Buttons => Page?.Buttons ?? Array.Empty<ButtonConfig>();

    /// <summary>Adopts a newly loaded configuration, keeping the current page where it still exists.</summary>
    /// <param name="configuration">The configuration to show.</param>
    public void Apply(DeckConfiguration configuration)
    {
        Configuration = configuration;
        _dispatcher.Timeout = TimeSpan.FromMilliseconds(Math.Max(1, configuration.App.Behaviour.ActionTimeoutMs));

        var previousProfileId = Profile?.Id;
        var previousPageId = Page?.Id;

        Profile = configuration.FindProfile(previousProfileId) ?? configuration.StartupProfile;
        Page = Profile?.Pages.FirstOrDefault(p => string.Equals(p.Id, previousPageId, StringComparison.OrdinalIgnoreCase))
               ?? Profile?.Pages.FirstOrDefault(p => !p.IsFolder)
               ?? Profile?.Pages.FirstOrDefault();
        Theme = configuration.ResolveTheme(Profile?.Theme);

        _logger.Information(
            "Showing profile {Profile} page {Page}",
            Profile?.Id ?? "(none)",
            Page?.Id ?? "(none)");

        SurfaceChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Handles a button being pressed.</summary>
    /// <param name="button">The button that was pressed.</param>
    public void Press(ButtonConfig button) => _dispatcher.Fire(button.Action);
}
