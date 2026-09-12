using TouchDeck.Core.Abstractions;
using TouchDeck.Core.Actions;
using TouchDeck.Core.Input;

namespace TouchDeck.Actions;

/// <summary>Which transport control a media action presses.</summary>
public enum MediaCommand
{
    PlayPause,
    Next,
    Previous,
    Stop,
}

/// <summary>
/// Presses one of the media keys, which whatever is playing picks up: Spotify, a browser
/// tab, anything that listens for them.
/// <code>
/// { "type": "media", "command": "playPause" }
/// </code>
/// </summary>
public sealed class MediaAction : IAction
{
    /// <inheritdoc />
    public string Type => "media";

    /// <inheritdoc />
    public string Title => "Control playback";

    /// <inheritdoc />
    public string Description => "Presses a media key, which whatever is playing picks up.";

    /// <inheritdoc />
    public IReadOnlyList<ActionParameter> Parameters { get; } = new[]
    {
        ActionParameter.OneOf("command", "Which control to press.", true, ActionContext.ChoiceList<MediaCommand>()),
    };

    /// <inheritdoc />
    public Task ExecuteAsync(ActionContext ctx, CancellationToken ct)
    {
        var key = ctx.RequireOneOf<MediaCommand>("command") switch
        {
            MediaCommand.Next => VirtualKey.MediaNextTrack,
            MediaCommand.Previous => VirtualKey.MediaPreviousTrack,
            MediaCommand.Stop => VirtualKey.MediaStop,
            _ => VirtualKey.MediaPlayPause,
        };

        ctx.Service<IInputInjector>().SendCombo(new KeyCombo(Array.Empty<VirtualKey>(), key));
        return Task.CompletedTask;
    }
}
