using TouchDeck.Core.Abstractions;
using TouchDeck.Core.Actions;

namespace TouchDeck.Actions;

/// <summary>
/// Tells OBS to do something over its websocket. Switch it on under settings first.
/// <code>
/// { "type": "obs", "command": "setScene", "scene": "Starting soon" }
/// { "type": "obs", "command": "toggleInputMute", "input": "Mic/Aux" }
/// { "type": "obs", "command": "setSourceVisible", "scene": "Main", "source": "Webcam", "enabled": false }
/// </code>
/// </summary>
public sealed class ObsAction : IAction
{
    /// <inheritdoc />
    public string Type => "obs";

    /// <inheritdoc />
    public string Title => "Tell OBS";

    /// <inheritdoc />
    public string Description => "Switches scene, mutes an input, starts recording, and the rest.";

    /// <inheritdoc />
    public IReadOnlyList<ActionParameter> Parameters { get; } = new[]
    {
        ActionParameter.OneOf("command", "What OBS should do.", true, ActionContext.ChoiceList<ObsCommand>()),
        ActionParameter.Optional("scene", ActionParameterKind.Text, "Scene name, for scene and source commands."),
        ActionParameter.Optional("input", ActionParameterKind.Text, "Input name, for mute and volume commands."),
        ActionParameter.Optional("source", ActionParameterKind.Text, "Source name, for filter and visibility commands."),
        ActionParameter.Optional("filter", ActionParameterKind.Text, "Filter name."),
        ActionParameter.Optional("enabled", ActionParameterKind.Boolean, "Whether the filter or source ends up on.", "true"),
        ActionParameter.Optional("volume", ActionParameterKind.Number, "Volume in decibels, where 0 is full and -100 is silent."),
    };

    /// <inheritdoc />
    public async Task ExecuteAsync(ActionContext ctx, CancellationToken ct)
    {
        var request = new ObsRequest(
            ctx.RequireOneOf<ObsCommand>("command"),
            ctx.Action.GetString("scene"),
            ctx.Action.GetString("input"),
            ctx.Action.GetString("source"),
            ctx.Action.GetString("filter"),
            ctx.Action.GetBoolean("enabled", true),
            ctx.Action.GetDouble("volume"));

        try
        {
            await ctx.Service<IObsControl>().SendAsync(request, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            throw new ActionException(ex.Message, ex);
        }
    }
}
