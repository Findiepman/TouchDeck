using TouchDeck.Core.Actions;

namespace TouchDeck.Actions;

/// <summary>
/// Shows another page of the current profile, either by name or by stepping.
/// <code>
/// { "type": "switchPage", "page": "scenes" }
/// { "type": "switchPage", "page": "next" }
/// </code>
/// </summary>
public sealed class SwitchPageAction : IAction
{
    /// <inheritdoc />
    public string Type => "switchPage";

    /// <inheritdoc />
    public string Title => "Switch page";

    /// <inheritdoc />
    public string Description => "Shows another page, by name or by stepping forwards and back.";

    /// <inheritdoc />
    public IReadOnlyList<ActionParameter> Parameters { get; } = new[]
    {
        ActionParameter.Require("page", ActionParameterKind.Text, "A page id, or the word next or previous."),
    };

    /// <inheritdoc />
    public Task ExecuteAsync(ActionContext ctx, CancellationToken ct)
    {
        var page = ctx.RequireString("page");

        if (page.Equals("next", StringComparison.OrdinalIgnoreCase))
        {
            ctx.Deck.NextPage();
        }
        else if (page.Equals("previous", StringComparison.OrdinalIgnoreCase)
                 || page.Equals("prev", StringComparison.OrdinalIgnoreCase))
        {
            ctx.Deck.PreviousPage();
        }
        else
        {
            ctx.Deck.SwitchPage(page);
        }

        return Task.CompletedTask;
    }
}
