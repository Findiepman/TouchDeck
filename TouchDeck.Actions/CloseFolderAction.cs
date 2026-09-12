using TouchDeck.Core.Actions;

namespace TouchDeck.Actions;

/// <summary>
/// Goes back to the page a folder was opened from. This is what a back button does.
/// <code>
/// { "type": "closeFolder" }
/// </code>
/// </summary>
public sealed class CloseFolderAction : IAction
{
    /// <inheritdoc />
    public string Type => "closeFolder";

    /// <inheritdoc />
    public string Title => "Go back";

    /// <inheritdoc />
    public string Description => "Returns to the page a folder was opened from.";

    /// <inheritdoc />
    public Task ExecuteAsync(ActionContext ctx, CancellationToken ct)
    {
        ctx.Deck.CloseFolder();
        return Task.CompletedTask;
    }
}
