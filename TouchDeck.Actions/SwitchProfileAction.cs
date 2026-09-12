using TouchDeck.Core.Actions;

namespace TouchDeck.Actions;

/// <summary>
/// Shows another profile. Doing this by hand pins the profile, so the foreground window
/// stops changing it underneath you until you switch back.
/// <code>
/// { "type": "switchProfile", "profile": "streaming" }
/// </code>
/// </summary>
public sealed class SwitchProfileAction : IAction
{
    /// <inheritdoc />
    public string Type => "switchProfile";

    /// <inheritdoc />
    public string Title => "Switch profile";

    /// <inheritdoc />
    public string Description => "Shows another profile and keeps it there until you switch again.";

    /// <inheritdoc />
    public IReadOnlyList<ActionParameter> Parameters { get; } = new[]
    {
        ActionParameter.Require("profile", ActionParameterKind.Text, "The id of the profile to show."),
    };

    /// <inheritdoc />
    public Task ExecuteAsync(ActionContext ctx, CancellationToken ct)
    {
        ctx.Deck.SwitchProfile(ctx.RequireString("profile"));
        return Task.CompletedTask;
    }
}
