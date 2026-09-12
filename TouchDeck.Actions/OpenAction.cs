using TouchDeck.Core.Abstractions;
using TouchDeck.Core.Actions;

namespace TouchDeck.Actions;

/// <summary>
/// Opens a web address in the default browser.
/// <code>
/// { "type": "open", "url": "https://dashboard.twitch.tv" }
/// </code>
/// </summary>
public sealed class OpenAction : IAction
{
    /// <inheritdoc />
    public string Type => "open";

    /// <inheritdoc />
    public string Title => "Open a link";

    /// <inheritdoc />
    public string Description => "Opens a web address in your default browser.";

    /// <inheritdoc />
    public IReadOnlyList<ActionParameter> Parameters { get; } = new[]
    {
        ActionParameter.Require("url", ActionParameterKind.Text, "The address to open."),
    };

    /// <inheritdoc />
    public Task ExecuteAsync(ActionContext ctx, CancellationToken ct)
    {
        var url = ctx.RequireString("url");

        if (!Uri.TryCreate(url, UriKind.Absolute, out var address))
        {
            throw new ActionException($"\"{url}\" is not a web address. It needs to start with https://.");
        }

        try
        {
            ctx.Service<IProcessLauncher>().Launch(new LaunchRequest(address.ToString()));
        }
        catch (InvalidOperationException ex)
        {
            throw new ActionException(ex.Message, ex);
        }

        return Task.CompletedTask;
    }
}
