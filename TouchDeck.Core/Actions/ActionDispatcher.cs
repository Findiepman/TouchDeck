using System.Diagnostics;
using Serilog;
using TouchDeck.Core.Configuration;

namespace TouchDeck.Core.Actions;

/// <summary>Reports an action that did not finish, so the UI can show it without knowing why.</summary>
/// <param name="Action">The action that failed.</param>
/// <param name="Message">A message suitable for showing the user.</param>
public sealed record ActionFailure(ActionConfig Action, string Message);

/// <summary>
/// Runs actions away from the UI thread, one failure never affecting another. Nothing here
/// throws back at the caller: failures are logged and raised as <see cref="Failed"/>.
/// </summary>
public sealed class ActionDispatcher
{
    private readonly ActionRegistry _registry;
    private readonly IServiceProvider _services;
    private readonly ILogger _logger;

    /// <summary>Creates a dispatcher.</summary>
    /// <param name="registry">Where action types are looked up.</param>
    /// <param name="services">Platform services handed to each action.</param>
    /// <param name="logger">Where failures are recorded.</param>
    public ActionDispatcher(ActionRegistry registry, IServiceProvider services, ILogger logger)
    {
        _registry = registry;
        _services = services;
        _logger = logger.ForContext<ActionDispatcher>();
    }

    /// <summary>Raised when an action fails or is cancelled.</summary>
    public event EventHandler<ActionFailure>? Failed;

    /// <summary>How long an action may run before it is cancelled. Updated when config reloads.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromMilliseconds(new BehaviourConfig().ActionTimeoutMs);

    /// <summary>
    /// Starts an action and returns immediately. Safe to call from the UI thread and safe to
    /// call with null, which does nothing.
    /// </summary>
    /// <param name="action">The action to run, or null.</param>
    public void Fire(ActionConfig? action)
    {
        if (action is null)
        {
            return;
        }

        _ = Task.Run(() => ExecuteAsync(action, CancellationToken.None));
    }

    /// <summary>Runs an action to completion, swallowing and reporting any failure.</summary>
    /// <param name="action">The action to run.</param>
    /// <param name="ct">Cancels the action early.</param>
    /// <returns>True when the action ran without error.</returns>
    public async Task<bool> ExecuteAsync(ActionConfig action, CancellationToken ct)
    {
        if (!_registry.TryGet(action.Type, out var implementation))
        {
            Report(action, $"There is no action type called \"{action.Type}\".", null);
            return false;
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(Timeout);

        var context = new ActionContext(action, _services, _logger.ForContext("ActionType", action.Type));
        var stopwatch = Stopwatch.StartNew();

        try
        {
            await implementation.ExecuteAsync(context, timeout.Token).ConfigureAwait(false);
            _logger.Debug("Action {Type} finished in {Elapsed}ms", action.Type, stopwatch.ElapsedMilliseconds);
            return true;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            Report(
                action,
                $"The \"{action.Type}\" action was still running after {Timeout.TotalSeconds:0.#}s and was cancelled.",
                null);
            return false;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (ActionException ex)
        {
            Report(action, ex.Message, ex);
            return false;
        }
        catch (Exception ex)
        {
            Report(action, $"The \"{action.Type}\" action failed: {ex.Message}", ex);
            return false;
        }
    }

    private void Report(ActionConfig action, string message, Exception? exception)
    {
        if (exception is null)
        {
            _logger.Warning("{Message} ({Action})", message, action);
        }
        else
        {
            _logger.Warning(exception, "{Message} ({Action})", message, action);
        }

        Failed?.Invoke(this, new ActionFailure(action, message));
    }
}
