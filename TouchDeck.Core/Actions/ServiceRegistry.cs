namespace TouchDeck.Core.Actions;

/// <summary>
/// A minimal service lookup, so actions can reach platform services without every action
/// growing a constructor. Registration happens once at startup.
/// </summary>
public sealed class ServiceRegistry : IServiceProvider
{
    private readonly Dictionary<Type, object> _services = new();

    /// <summary>Registers <paramref name="implementation"/> under the contract <typeparamref name="TService"/>.</summary>
    /// <typeparam name="TService">The contract actions ask for.</typeparam>
    /// <param name="implementation">The implementation to hand out.</param>
    public ServiceRegistry Add<TService>(TService implementation)
        where TService : class
    {
        _services[typeof(TService)] = implementation;
        return this;
    }

    /// <inheritdoc />
    public object? GetService(Type serviceType) =>
        _services.TryGetValue(serviceType, out var service) ? service : null;
}

/// <summary>Convenience lookups over <see cref="IServiceProvider"/>.</summary>
public static class ServiceProviderExtensions
{
    /// <summary>Gets a registered service, or throws a message naming what is missing.</summary>
    /// <typeparam name="TService">The contract to resolve.</typeparam>
    /// <param name="services">The provider to look in.</param>
    public static TService GetRequired<TService>(this IServiceProvider services)
        where TService : class =>
        services.GetService(typeof(TService)) as TService
        ?? throw new InvalidOperationException($"No {typeof(TService).Name} has been registered.");
}
