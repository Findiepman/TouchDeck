using Serilog;
using TouchDeck.Core.Configuration;

namespace TouchDeck.Core.Actions;

/// <summary>
/// One thing a button can do. Implementations live in TouchDeck.Actions, one per file, and
/// are discovered by assembly scanning: adding an action type means adding a file and
/// nothing else.
/// </summary>
/// <remarks>
/// Implementations are created once and reused for every press, so they must be stateless
/// and safe to call from several threads at the same time.
/// </remarks>
public interface IAction
{
    /// <summary>The <c>type</c> discriminator this action answers to, for example <c>hotkey</c>.</summary>
    string Type { get; }

    /// <summary>Runs the action.</summary>
    /// <param name="ctx">Parameters and services for this invocation.</param>
    /// <param name="ct">Cancelled when the action outlives its timeout.</param>
    Task ExecuteAsync(ActionContext ctx, CancellationToken ct);
}

/// <summary>Everything an action needs for one invocation.</summary>
public sealed class ActionContext
{
    /// <summary>Creates a context.</summary>
    /// <param name="action">The action as written in config, including its parameters.</param>
    /// <param name="services">Platform services available to actions.</param>
    /// <param name="logger">Logger already tagged with the action type.</param>
    public ActionContext(ActionConfig action, IServiceProvider services, ILogger logger)
    {
        Action = action;
        Services = services;
        Logger = logger;
    }

    /// <summary>The action as written in config, including its parameters.</summary>
    public ActionConfig Action { get; }

    /// <summary>Platform services available to actions.</summary>
    public IServiceProvider Services { get; }

    /// <summary>Logger for this invocation.</summary>
    public ILogger Logger { get; }

    /// <summary>Resolves a platform service, throwing if it was never registered.</summary>
    /// <typeparam name="TService">The contract to resolve.</typeparam>
    public TService Service<TService>()
        where TService : class => Services.GetRequired<TService>();

    /// <summary>Creates a context for a nested action, reusing the same services.</summary>
    /// <param name="action">The nested action.</param>
    public ActionContext ForNested(ActionConfig action) =>
        new(action, Services, Logger.ForContext("ActionType", action.Type));

    /// <summary>Reads a required string parameter, or throws a message the user can act on.</summary>
    /// <param name="name">Parameter name as written in JSON.</param>
    public string RequireString(string name) =>
        Action.GetString(name) is { Length: > 0 } value
            ? value
            : throw new ActionException($"The \"{Action.Type}\" action needs a \"{name}\" value.");
}

/// <summary>
/// Thrown when an action cannot run because of how it was configured. The message is shown
/// to the user, so it should say what to fix.
/// </summary>
public sealed class ActionException : Exception
{
    /// <summary>Creates an exception with a user facing message.</summary>
    /// <param name="message">What went wrong, in plain words.</param>
    public ActionException(string message)
        : base(message)
    {
    }

    /// <summary>Creates an exception with a user facing message and an underlying cause.</summary>
    /// <param name="message">What went wrong, in plain words.</param>
    /// <param name="inner">The underlying failure.</param>
    public ActionException(string message, Exception inner)
        : base(message, inner)
    {
    }
}
