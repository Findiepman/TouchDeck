using Serilog;
using TouchDeck.Core.Abstractions;
using TouchDeck.Core.Configuration;
using TouchDeck.Core.Expressions;
using TouchDeck.Core.Variables;

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

    /// <summary>
    /// A short name for the action picker in the config center. Defaults to
    /// <see cref="Type"/>, so an action that does not care can ignore it.
    /// </summary>
    string Title => Type;

    /// <summary>One line saying what the action does, shown next to its name in the picker.</summary>
    string Description => string.Empty;

    /// <summary>
    /// The parameters this action reads. The config center builds its form from these, which
    /// is why a new action type needs no editor changes at all.
    /// </summary>
    IReadOnlyList<ActionParameter> Parameters => Array.Empty<ActionParameter>();

    /// <summary>Runs the action.</summary>
    /// <param name="ctx">Parameters and services for this invocation.</param>
    /// <param name="ct">Cancelled when the action outlives its timeout.</param>
    Task ExecuteAsync(ActionContext ctx, CancellationToken ct);
}

/// <summary>Everything an action needs for one invocation.</summary>
public sealed class ActionContext
{
    private readonly Func<ActionConfig, CancellationToken, Task<bool>>? _runNested;

    /// <summary>Creates a context.</summary>
    /// <param name="action">The action as written in config, including its parameters.</param>
    /// <param name="services">Platform services available to actions.</param>
    /// <param name="logger">Logger already tagged with the action type.</param>
    /// <param name="runNested">Runs a nested action, for the actions that compose others.</param>
    public ActionContext(
        ActionConfig action,
        IServiceProvider services,
        ILogger logger,
        Func<ActionConfig, CancellationToken, Task<bool>>? runNested = null)
    {
        Action = action;
        Services = services;
        Logger = logger;
        _runNested = runNested;
    }

    /// <summary>The action as written in config, including its parameters.</summary>
    public ActionConfig Action { get; }

    /// <summary>Platform services available to actions.</summary>
    public IServiceProvider Services { get; }

    /// <summary>Logger for this invocation.</summary>
    public ILogger Logger { get; }

    /// <summary>Changes what the panel is showing.</summary>
    public IDeckController Deck => Service<IDeckController>();

    /// <summary>Values the user sets from buttons.</summary>
    public IVariableStore Variables => Service<IVariableStore>();

    /// <summary>Where a condition looks up the names it refers to.</summary>
    public IValueResolver Values => Service<IValueResolver>();

    /// <summary>Resolves a platform service, throwing if it was never registered.</summary>
    /// <typeparam name="TService">The contract to resolve.</typeparam>
    public TService Service<TService>()
        where TService : class => Services.GetRequired<TService>();

    /// <summary>Creates a context for a nested action, reusing the same services.</summary>
    /// <param name="action">The nested action.</param>
    public ActionContext ForNested(ActionConfig action) =>
        new(action, Services, Logger.ForContext("ActionType", action.Type), _runNested);

    /// <summary>
    /// Runs another action as part of this one, with the same services and the same error
    /// isolation. Used by sequence, conditional and random.
    /// </summary>
    /// <param name="action">The action to run.</param>
    /// <param name="ct">Cancels the nested action.</param>
    /// <returns>True when it ran without error.</returns>
    public Task<bool> RunAsync(ActionConfig action, CancellationToken ct) =>
        _runNested?.Invoke(action, ct)
        ?? throw new ActionException("This action cannot run other actions here.");

    /// <summary>Reads a required string parameter, or throws a message the user can act on.</summary>
    /// <param name="name">Parameter name as written in JSON.</param>
    public string RequireString(string name) =>
        Action.GetString(name) is { Length: > 0 } value
            ? value
            : throw new ActionException($"The \"{Action.Type}\" action needs a \"{name}\" value.");

    /// <summary>Reads a parameter that must be one of a fixed set, or throws naming the set.</summary>
    /// <typeparam name="TEnum">The set of allowed values.</typeparam>
    /// <param name="name">Parameter name as written in JSON.</param>
    /// <param name="fallback">Used when the parameter is absent. Null makes it required.</param>
    public TEnum RequireOneOf<TEnum>(string name, TEnum? fallback = null)
        where TEnum : struct, Enum
    {
        var written = Action.GetString(name);

        if (string.IsNullOrWhiteSpace(written))
        {
            return fallback ?? throw new ActionException(
                $"The \"{Action.Type}\" action needs a \"{name}\" value, one of: {Choices<TEnum>()}.");
        }

        if (Enum.TryParse<TEnum>(written, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed))
        {
            return parsed;
        }

        throw new ActionException(
            $"\"{written}\" is not a {name} the \"{Action.Type}\" action knows. Try one of: {Choices<TEnum>()}.");
    }

    /// <summary>The allowed values of an enum, written the way config writes them.</summary>
    /// <typeparam name="TEnum">The enum to list.</typeparam>
    public static string Choices<TEnum>()
        where TEnum : struct, Enum =>
        string.Join(", ", ChoiceList<TEnum>());

    /// <summary>The allowed values of an enum as an array, for action metadata.</summary>
    /// <typeparam name="TEnum">The enum to list.</typeparam>
    public static string[] ChoiceList<TEnum>()
        where TEnum : struct, Enum =>
        Enum.GetNames<TEnum>().Select(Camel).ToArray();

    private static string Camel(string name) =>
        name.Length == 0 ? name : char.ToLowerInvariant(name[0]) + name[1..];
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
