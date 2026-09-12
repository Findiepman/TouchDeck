using TouchDeck.Core.Actions;

namespace TouchDeck.Actions;

/// <summary>
/// Picks one of several actions at random.
/// <code>
/// { "type": "random", "from": [
///     { "type": "text", "value": "gg" },
///     { "type": "text", "value": "well played" }
/// ] }
/// </code>
/// </summary>
public sealed class RandomAction : IAction
{
    /// <inheritdoc />
    public string Type => "random";

    /// <inheritdoc />
    public string Title => "Pick one at random";

    /// <inheritdoc />
    public string Description => "Runs one of several actions, chosen at random.";

    /// <inheritdoc />
    public async Task ExecuteAsync(ActionContext ctx, CancellationToken ct)
    {
        var choices = ctx.Action.GetActions("from");

        if (choices.Count == 0)
        {
            throw new ActionException("This button has nothing to choose from, so pressing it does nothing.");
        }

        var picked = choices[Random.Shared.Next(choices.Count)];

        ctx.Logger.Debug("Picked {Type} out of {Count}", picked.Type, choices.Count);
        await ctx.RunAsync(picked, ct).ConfigureAwait(false);
    }
}
