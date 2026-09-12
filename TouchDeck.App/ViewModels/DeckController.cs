using System.Windows.Threading;
using TouchDeck.Core.Abstractions;

namespace TouchDeck.App.ViewModels;

/// <summary>
/// Lets actions change what the panel shows. Actions run on the thread pool and the panel
/// belongs to the UI thread, so every call hops across.
/// </summary>
public sealed class DeckController : IDeckController
{
    private readonly DeckViewModel _deck;
    private readonly Dispatcher _dispatcher;

    /// <summary>Creates a controller over a deck.</summary>
    /// <param name="deck">What is on screen.</param>
    /// <param name="dispatcher">The UI thread the deck belongs to.</param>
    public DeckController(DeckViewModel deck, Dispatcher dispatcher)
    {
        _deck = deck;
        _dispatcher = dispatcher;
    }

    /// <inheritdoc />
    public string? CurrentProfileId => _deck.Profile?.Id;

    /// <inheritdoc />
    public string? CurrentPageId => _deck.Page?.Id;

    /// <inheritdoc />
    public void SwitchProfile(string profileId) => OnUiThread(() => _deck.SwitchProfile(profileId));

    /// <inheritdoc />
    public void SwitchPage(string pageId) => OnUiThread(() => _deck.SwitchPage(pageId));

    /// <inheritdoc />
    public void NextPage() => OnUiThread(() => _deck.StepPage(forwards: true));

    /// <inheritdoc />
    public void PreviousPage() => OnUiThread(() => _deck.StepPage(forwards: false));

    /// <inheritdoc />
    public void OpenFolder(string pageId) => OnUiThread(() => _deck.OpenFolder(pageId));

    /// <inheritdoc />
    public void CloseFolder() => OnUiThread(_deck.CloseFolder);

    /// <summary>
    /// Runs on the UI thread and waits, so an action that switches page and then presses a
    /// key sees the new page first.
    /// </summary>
    private void OnUiThread(Action work)
    {
        if (_dispatcher.CheckAccess())
        {
            work();
            return;
        }

        _dispatcher.Invoke(work, DispatcherPriority.Send);
    }
}
