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

    /// <summary>Pages a folder was opened from, so a back button knows where to return to.</summary>
    private readonly Stack<string> _cameFrom = new();

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

    /// <summary>
    /// True once a profile was chosen by hand, which stops the foreground window changing it
    /// underneath you.
    /// </summary>
    public bool IsProfilePinned { get; private set; }

    /// <summary>
    /// Buttons on the current page, with the back button included when the page is a folder
    /// that declares one.
    /// </summary>
    public IReadOnlyList<ButtonConfig> Buttons
    {
        get
        {
            if (Page is not { } page)
            {
                return Array.Empty<ButtonConfig>();
            }

            if (page.BackButton is not { } back)
            {
                return page.Buttons;
            }

            return page.Buttons.Append(back).ToArray();
        }
    }

    /// <summary>Adopts a newly loaded configuration, keeping the current page where it still exists.</summary>
    /// <param name="configuration">The configuration to show.</param>
    public void Apply(DeckConfiguration configuration)
    {
        Configuration = configuration;
        _dispatcher.Timeout = TimeSpan.FromMilliseconds(Math.Max(1, configuration.App.Behaviour.ActionTimeoutMs));

        var previousProfileId = Profile?.Id;
        var previousPageId = Page?.Id;

        Profile = configuration.FindProfile(previousProfileId) ?? configuration.StartupProfile;
        Page = FindPage(previousPageId) ?? FirstPage();
        Theme = configuration.ResolveTheme(Profile?.Theme);

        _logger.Information(
            "Showing profile {Profile} page {Page}",
            Profile?.Id ?? "(none)",
            Page?.Id ?? "(none)");

        Announce();
    }

    /// <summary>Shows another profile, and keeps it there until asked otherwise.</summary>
    /// <param name="profileId">Id of the profile to show.</param>
    /// <param name="pin">Whether this counts as a manual choice.</param>
    /// <returns>False when no profile has that id.</returns>
    public bool SwitchProfile(string profileId, bool pin = true)
    {
        if (Configuration.FindProfile(profileId) is not { } profile)
        {
            _logger.Warning("No profile has the id {Profile}, so nothing changed.", profileId);
            return false;
        }

        if (ReferenceEquals(profile, Profile))
        {
            IsProfilePinned |= pin;
            return true;
        }

        Profile = profile;
        Theme = Configuration.ResolveTheme(profile.Theme);
        _cameFrom.Clear();
        Page = FirstPage();
        IsProfilePinned |= pin;

        _logger.Information("Switched to profile {Profile}", profile.Id);
        Announce();
        return true;
    }

    /// <summary>Stops pinning, so the foreground window can choose the profile again.</summary>
    public void UnpinProfile()
    {
        IsProfilePinned = false;
        Announce();
    }

    /// <summary>Shows a page of the current profile.</summary>
    /// <param name="pageId">Id of the page to show.</param>
    /// <returns>False when the current profile has no such page.</returns>
    public bool SwitchPage(string pageId)
    {
        if (FindPage(pageId) is not { } page)
        {
            _logger.Warning("No page in {Profile} has the id {Page}.", Profile?.Id, pageId);
            return false;
        }

        if (ReferenceEquals(page, Page))
        {
            return true;
        }

        Page = page;
        _cameFrom.Clear();
        Announce();
        return true;
    }

    /// <summary>Steps through the pages that are not folders, wrapping at the ends.</summary>
    /// <param name="forwards">True to go to the next page, false for the previous one.</param>
    public void StepPage(bool forwards)
    {
        var pages = Swipeable();

        if (pages.Count < 2 || Page is null)
        {
            return;
        }

        var at = pages.FindIndex(p => ReferenceEquals(p, Page));

        if (at < 0)
        {
            // Currently inside a folder, so stepping leaves it at the page it came from.
            at = 0;
        }

        var next = ((at + (forwards ? 1 : -1)) % pages.Count + pages.Count) % pages.Count;

        Page = pages[next];
        _cameFrom.Clear();
        Announce();
    }

    /// <summary>Opens a folder page, remembering where to come back to.</summary>
    /// <param name="pageId">Id of the folder page.</param>
    /// <returns>False when the current profile has no such page.</returns>
    public bool OpenFolder(string pageId)
    {
        if (FindPage(pageId) is not { } page)
        {
            _logger.Warning("No page in {Profile} has the id {Page}.", Profile?.Id, pageId);
            return false;
        }

        if (Page is { } current && !ReferenceEquals(current, page))
        {
            _cameFrom.Push(current.Id);
        }

        Page = page;
        Announce();
        return true;
    }

    /// <summary>Returns from a folder, or to the first ordinary page when nothing was remembered.</summary>
    public void CloseFolder()
    {
        var target = _cameFrom.Count > 0 ? FindPage(_cameFrom.Pop()) : null;

        Page = target ?? Swipeable().FirstOrDefault() ?? Page;
        Announce();
    }

    /// <summary>Handles a button being pressed.</summary>
    /// <param name="button">The button that was pressed.</param>
    public void Press(ButtonConfig button) => _dispatcher.Fire(button.Action);

    /// <summary>Pages a swipe or a next and previous step moves between.</summary>
    private List<Page> Swipeable() =>
        Profile?.Pages.Where(p => !p.IsFolder).ToList() ?? new List<Page>();

    private Page? FindPage(string? pageId) =>
        pageId is null
            ? null
            : Profile?.Pages.FirstOrDefault(p => string.Equals(p.Id, pageId, StringComparison.OrdinalIgnoreCase));

    private Page? FirstPage() =>
        Profile?.Pages.FirstOrDefault(p => !p.IsFolder) ?? Profile?.Pages.FirstOrDefault();

    private void Announce() => SurfaceChanged?.Invoke(this, EventArgs.Empty);
}
