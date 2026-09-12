namespace TouchDeck.Core.Abstractions;

/// <summary>
/// Lets an action change what the panel is showing. Implementations are called from action
/// threads, so they must marshal onto the UI thread themselves.
/// </summary>
public interface IDeckController
{
    /// <summary>Id of the profile on screen, or null when nothing loaded.</summary>
    string? CurrentProfileId { get; }

    /// <summary>Id of the page on screen, or null when nothing loaded.</summary>
    string? CurrentPageId { get; }

    /// <summary>
    /// Shows a profile and pins it, which stops the foreground window from switching it
    /// back until the user unpins.
    /// </summary>
    /// <param name="profileId">Id of the profile to show.</param>
    void SwitchProfile(string profileId);

    /// <summary>Shows a page in the current profile.</summary>
    /// <param name="pageId">Id of the page to show.</param>
    void SwitchPage(string pageId);

    /// <summary>Shows the next page, wrapping at the end. Folders are skipped.</summary>
    void NextPage();

    /// <summary>Shows the previous page, wrapping at the start. Folders are skipped.</summary>
    void PreviousPage();

    /// <summary>Opens a folder page, remembering where to come back to.</summary>
    /// <param name="pageId">Id of the folder page.</param>
    void OpenFolder(string pageId);

    /// <summary>Returns from a folder to the page it was opened from.</summary>
    void CloseFolder();
}
