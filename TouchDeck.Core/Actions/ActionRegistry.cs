using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Serilog;

namespace TouchDeck.Core.Actions;

/// <summary>
/// Finds every <see cref="IAction"/> in the given assemblies and indexes it by its type
/// name. Nothing else knows the list of action types, which is what keeps adding one to a
/// single new file.
/// </summary>
public sealed class ActionRegistry
{
    private readonly Dictionary<string, IAction> _actions = new(StringComparer.OrdinalIgnoreCase);

    private ActionRegistry()
    {
    }

    /// <summary>Every action type the deck can execute.</summary>
    public IReadOnlyCollection<string> KnownTypes => _actions.Keys;

    /// <summary>
    /// Every registered action, so the config center can read their names and parameters
    /// instead of keeping its own list.
    /// </summary>
    public IReadOnlyCollection<IAction> All => _actions.Values;

    /// <summary>Scans assemblies for action implementations.</summary>
    /// <param name="logger">Receives a warning for anything that could not be registered.</param>
    /// <param name="assemblies">Assemblies to scan. Defaults to the calling assembly.</param>
    public static ActionRegistry Scan(ILogger logger, params Assembly[] assemblies)
    {
        var registry = new ActionRegistry();

        foreach (var assembly in assemblies)
        {
            foreach (var type in GetLoadableTypes(assembly, logger))
            {
                if (!typeof(IAction).IsAssignableFrom(type) || type.IsAbstract || type.IsInterface)
                {
                    continue;
                }

                if (type.GetConstructor(Type.EmptyTypes) is null)
                {
                    logger.Warning(
                        "Action {Action} was skipped because it has no parameterless constructor.",
                        type.FullName);
                    continue;
                }

                IAction instance;
                try
                {
                    instance = (IAction)Activator.CreateInstance(type)!;
                }
                catch (Exception ex)
                {
                    logger.Warning(ex, "Action {Action} could not be created.", type.FullName);
                    continue;
                }

                if (string.IsNullOrWhiteSpace(instance.Type))
                {
                    logger.Warning("Action {Action} was skipped because its Type is empty.", type.FullName);
                    continue;
                }

                if (registry._actions.TryGetValue(instance.Type, out var existing))
                {
                    logger.Warning(
                        "Action type {Type} is claimed by both {First} and {Second}. Keeping {First}.",
                        instance.Type,
                        existing.GetType().FullName,
                        type.FullName);
                    continue;
                }

                registry._actions[instance.Type] = instance;
            }
        }

        logger.Information("Registered {Count} action types: {Types}", registry._actions.Count, registry.KnownTypes);
        return registry;
    }

    /// <summary>Looks up an action by its <c>type</c> discriminator.</summary>
    /// <param name="type">The discriminator from config.</param>
    /// <param name="action">The matching action when one exists.</param>
    public bool TryGet(string type, [NotNullWhen(true)] out IAction? action) =>
        _actions.TryGetValue(type, out action);

    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly, ILogger logger)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            logger.Warning(ex, "Some types in {Assembly} could not be loaded.", assembly.FullName);
            return ex.Types.Where(t => t is not null)!;
        }
    }
}
