namespace TouchDeck.Core.Configuration;

/// <summary>
/// Fills in the bookkeeping the JSON cannot carry: json paths on every action, so that a
/// failing action can be pointed at the exact place in the file it came from.
/// </summary>
public static class ProfileNormaliser
{
    /// <summary>Returns a copy of <paramref name="profile"/> with action paths filled in.</summary>
    public static Profile Normalise(Profile profile)
    {
        var pages = new List<Page>(profile.Pages.Count);

        for (var pageIndex = 0; pageIndex < profile.Pages.Count; pageIndex++)
        {
            var page = profile.Pages[pageIndex];
            var pagePath = $"pages[{pageIndex}]";
            var buttons = new List<ButtonConfig>(page.Buttons.Count);

            for (var buttonIndex = 0; buttonIndex < page.Buttons.Count; buttonIndex++)
            {
                buttons.Add(NormaliseButton(page.Buttons[buttonIndex], $"{pagePath}.buttons[{buttonIndex}]"));
            }

            pages.Add(page with
            {
                Buttons = buttons,
                BackButton = page.BackButton is null
                    ? null
                    : NormaliseButton(page.BackButton, $"{pagePath}.backButton"),
            });
        }

        return profile with { Pages = pages };
    }

    private static ButtonConfig NormaliseButton(ButtonConfig button, string path) => button with
    {
        Action = WithPath(button.Action, $"{path}.action"),
        ReleaseAction = WithPath(button.ReleaseAction, $"{path}.releaseAction"),
        LongPressAction = WithPath(button.LongPressAction, $"{path}.longPressAction"),
        DoubleTapAction = WithPath(button.DoubleTapAction, $"{path}.doubleTapAction"),
    };

    private static ActionConfig? WithPath(ActionConfig? action, string path) =>
        action is null ? null : action with { JsonPath = path };
}
