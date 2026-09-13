using TouchDeck.Core.Abstractions;
using TouchDeck.Core.Actions;

namespace TouchDeck.Actions;

/// <summary>
/// Plays a clip from a QuoteDeck category. Nothing is synthesised at press time: the clips
/// were rendered ahead of time by <c>quotedeck build</c>.
/// <code>
/// { "type": "soundboard", "category": "insults" }
/// { "type": "soundboard", "category": "insults", "id": "rage",
///   "devices": ["CABLE Input", "Koptelefoon"], "volume": 0.9, "policy": "cutoff" }
/// </code>
/// </summary>
/// <remarks>
/// With no <c>id</c> the button draws from a shuffle bag, so every line in the category is
/// heard before any repeats and the same one never plays twice in a row.
/// <para>
/// Listing two devices plays the clip to both at once, which is how it reaches a virtual
/// microphone and the user's own headphones together. The word <c>default</c> stands for
/// whatever Windows is playing through, so the second copy does not have to name a headset
/// that might not be the one plugged in.
/// </para>
/// </remarks>
public sealed class SoundboardAction : IAction
{
    /// <inheritdoc />
    public string Type => "soundboard";

    /// <inheritdoc />
    public string Title => "Play a quote";

    /// <inheritdoc />
    public string Description => "Plays a clip from a QuoteDeck category, shuffled without repeats.";

    /// <inheritdoc />
    public IReadOnlyList<ActionParameter> Parameters { get; } = new[]
    {
        ActionParameter.Require("category", ActionParameterKind.Text, "The QuoteDeck category to play from."),
        ActionParameter.Optional("id", ActionParameterKind.Text, "One specific clip, instead of a shuffled one."),
        ActionParameter.Optional("devices", ActionParameterKind.Text, "Output device names to play to at once, where \"default\" means whatever Windows is using. Leave empty for the default device."),
        ActionParameter.Optional("volume", ActionParameterKind.Number, "Playback volume from 0 to 1.", "0.9"),
        ActionParameter.OneOf("policy", "What a press does while a clip is already playing.", false, ActionContext.ChoiceList<SoundboardPolicy>()),
    };

    /// <inheritdoc />
    public Task ExecuteAsync(ActionContext ctx, CancellationToken ct)
    {
        var request = new SoundboardRequest(
            ctx.RequireString("category"),
            ctx.Action.GetString("id"),
            ReadDevices(ctx),
            ctx.Action.GetDouble("volume", 0.9),
            ctx.RequireOneOf("policy", SoundboardPolicy.Cutoff));

        var result = ctx.Service<ISoundboardPlayer>().Play(request);

        if (result.Quiet)
        {
            return Task.CompletedTask;
        }

        // Everything below is a real problem the user can fix. Log it and let the dispatcher
        // put the button into its error state; never let a raw exception reach the UI.
        ctx.Logger.Warning("Soundboard: {Detail}", result.Detail);
        throw new ActionException(result.Detail);
    }

    /// <summary>
    /// Reads <c>devices</c>, which people write as an array but sometimes as one string.
    /// Both mean the same thing, so both are accepted.
    /// </summary>
    private static IReadOnlyList<string> ReadDevices(ActionContext ctx)
    {
        if (!ctx.Action.TryGetParameter("devices", out var value))
        {
            return Array.Empty<string>();
        }

        if (value.ValueKind == System.Text.Json.JsonValueKind.String)
        {
            var single = value.GetString();
            return string.IsNullOrWhiteSpace(single) ? Array.Empty<string>() : new[] { single.Trim() };
        }

        if (value.ValueKind != System.Text.Json.JsonValueKind.Array)
        {
            return Array.Empty<string>();
        }

        var names = new List<string>();
        foreach (var item in value.EnumerateArray())
        {
            if (item.ValueKind == System.Text.Json.JsonValueKind.String
                && item.GetString() is { Length: > 0 } name
                && !string.IsNullOrWhiteSpace(name))
            {
                names.Add(name.Trim());
            }
        }

        return names;
    }
}
