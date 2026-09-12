using TouchDeck.Core.Abstractions;
using TouchDeck.Core.Actions;

namespace TouchDeck.Actions;

/// <summary>
/// Changes volume or mute, either for a whole device or for one application's slider in the
/// volume mixer.
/// <code>
/// { "type": "audio", "target": "default", "operation": "toggleMute" }
/// { "type": "audio", "target": "process", "name": "chrome", "operation": "adjust", "value": -0.1 }
/// </code>
/// </summary>
public sealed class AudioAction : IAction
{
    /// <inheritdoc />
    public string Type => "audio";

    /// <inheritdoc />
    public string Title => "Change volume";

    /// <inheritdoc />
    public string Description => "Mutes or changes the volume of a device or one application.";

    /// <inheritdoc />
    public IReadOnlyList<ActionParameter> Parameters { get; } = new[]
    {
        ActionParameter.OneOf("target", "Whose volume to change.", false, ActionContext.ChoiceList<AudioTarget>()),
        ActionParameter.Optional("name", ActionParameterKind.Text, "Device or program name, when the target needs one."),
        ActionParameter.OneOf("operation", "What to do to it.", true, ActionContext.ChoiceList<AudioOperation>()),
        ActionParameter.Optional("value", ActionParameterKind.Number, "Level from 0 to 1 when setting, or the amount to move by when adjusting."),
    };

    /// <inheritdoc />
    public Task ExecuteAsync(ActionContext ctx, CancellationToken ct)
    {
        var target = ctx.RequireOneOf("target", AudioTarget.Default);
        var operation = ctx.RequireOneOf<AudioOperation>("operation");
        var name = ctx.Action.GetString("name");

        if (target != AudioTarget.Default && string.IsNullOrWhiteSpace(name))
        {
            throw new ActionException(
                $"Changing the volume of a {target.ToString().ToLowerInvariant()} needs its \"name\".");
        }

        try
        {
            ctx.Service<IAudioMixer>().Apply(target, name, operation, ctx.Action.GetDouble("value"));
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            throw new ActionException(ex.Message, ex);
        }

        return Task.CompletedTask;
    }
}
