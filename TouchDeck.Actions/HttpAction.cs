using TouchDeck.Core.Abstractions;
using TouchDeck.Core.Actions;
using TouchDeck.Core.Variables;

namespace TouchDeck.Actions;

/// <summary>
/// Sends a web request, which is how a button talks to anything with an API the deck has no
/// action for: a smart bulb, a home server, a bot.
/// <code>
/// { "type": "http", "method": "POST", "url": "http://192.168.1.50/api/lights/1",
///   "headers": { "Authorization": "Bearer abc" },
///   "body": "{ \"on\": true }",
///   "saveResponseTo": "lightState" }
/// </code>
/// </summary>
public sealed class HttpAction : IAction
{
    /// <inheritdoc />
    public string Type => "http";

    /// <inheritdoc />
    public string Title => "Send a web request";

    /// <inheritdoc />
    public string Description => "Calls a web API, for anything the deck has no action for.";

    /// <inheritdoc />
    public IReadOnlyList<ActionParameter> Parameters { get; } = new[]
    {
        ActionParameter.Require("url", ActionParameterKind.Text, "Where to send it."),
        ActionParameter.OneOf("method", "Which verb to use.", false, "GET", "POST", "PUT", "PATCH", "DELETE"),
        ActionParameter.Optional("body", ActionParameterKind.MultilineText, "What to send, for the verbs that carry a body."),
        ActionParameter.Optional("saveResponseTo", ActionParameterKind.Text, "Name of a variable to keep the answer in."),
    };

    /// <inheritdoc />
    public async Task ExecuteAsync(ActionContext ctx, CancellationToken ct)
    {
        string answer;

        try
        {
            answer = await ctx.Service<IHttpSender>().SendAsync(
                ctx.Action.GetString("method", "GET"),
                ctx.RequireString("url"),
                ctx.Action.GetMap("headers"),
                ctx.Action.GetString("body"),
                ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not ActionException)
        {
            throw new ActionException($"The request failed: {ex.Message}", ex);
        }

        if (ctx.Action.GetString("saveResponseTo") is { Length: > 0 } into)
        {
            ctx.Variables.Set(VariableName.Of(into), answer, VariableScope.Session);
        }
    }
}
