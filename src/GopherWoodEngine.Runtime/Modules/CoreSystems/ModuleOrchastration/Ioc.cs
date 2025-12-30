using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;

namespace GopherWoodEngine.Runtime.Modules;

/// <summary>
/// A type that facilitates the use of the <see cref="IServiceProvider"/> type.
/// The <see cref="Ioc"/> provides the ability to configure services in a singleton, thread-safe
/// service provider instance, which can then be used to resolve service instances from anywhere in the application.
/// </summary>
/// <remarks>
/// Modeled after the CommunityToolkit offering <see href="https://github.com/CommunityToolkit/dotnet/blob/main/src/CommunityToolkit.Mvvm/DependencyInjection/Ioc.cs">here</see>.
/// </remarks>
public sealed partial class Ioc : IServiceProvider
{
    /// <summary>
    /// Gets the default <see cref="Ioc"/> instance.
    /// </summary>
    public static Ioc Default { get; } = new();

    /// <summary>
    /// The <see cref="IServiceProvider"/> instance to use, if initialized.
    /// </summary>
    private volatile IServiceProvider? serviceProvider;

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="serviceType"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">Thrown if the service provider has not been configured.</exception>
    public object? GetService(Type serviceType)
    {
        ArgumentNullException.ThrowIfNull(serviceType);

        IServiceProvider? provider = this.serviceProvider;

        if (provider is null)
        {
            ThrowInvalidOperationExceptionForMissingInitialization();
        }

        return provider.GetService(serviceType);
    }

    /// <summary>
    /// Resolves an instance of a specified service type.
    /// </summary>
    /// <typeparam name="T">The type of service object to resolve.</typeparam>
    /// <returns>An instance of the specified service, or <see langword="null"/>.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the service provider has not been configured.</exception>
    public T? GetService<T>() where T : class
    {
        IServiceProvider? provider = this.serviceProvider;

        if (provider is null)
        {
            ThrowInvalidOperationExceptionForMissingInitialization();
        }

        return (T?)provider.GetService(typeof(T));
    }

    /// <summary>
    /// Resolves an instance of a specified service type.
    /// </summary>
    /// <typeparam name="T">The type of service to resolve.</typeparam>
    /// <returns>An instance of the specified service, or <see langword="null"/>.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the current <see cref="Ioc"/> instance has not been initialized, or if the
    /// requested service type was not registered in the service provider currently in use.
    /// </exception>
    public T GetRequiredService<T>() where T : class
    {
        T? service = GetService<T>();

        if (service is null)
        {
            ThrowInvalidOperationExceptionForUnregisteredType();
        }

        return service!;
    }

    /// <summary>
    /// Initializes the shared <see cref="IServiceProvider"/> instance.
    /// </summary>
    /// <param name="serviceProvider">The input <see cref="IServiceProvider"/> instance to use.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="serviceProvider"/> is <see langword="null"/>.</exception>
    public void ConfigureServices(IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);

        IServiceProvider? oldServices = Interlocked.CompareExchange(ref this.serviceProvider, serviceProvider, null);

        if (oldServices is not null)
        {
            ThrowInvalidOperationExceptionForRepeatedConfiguration();
        }
    }

    /// <summary>
    /// Throws an <see cref="InvalidOperationException"/> when the <see cref="IServiceProvider"/> property is used before initialization.
    /// </summary>
    [DoesNotReturn]
    private static void ThrowInvalidOperationExceptionForMissingInitialization()
    {
        throw new InvalidOperationException("The service provider has not been configured yet.");
    }

    /// <summary>
    /// Throws an <see cref="InvalidOperationException"/> when the <see cref="IServiceProvider"/> property is missing a type registration.
    /// </summary>
    [DoesNotReturn]
    private static void ThrowInvalidOperationExceptionForUnregisteredType()
    {
        throw new InvalidOperationException("The requested service type was not registered.");
    }

    /// <summary>
    /// Throws an <see cref="InvalidOperationException"/> when a configuration is attempted more than once.
    /// </summary>
    [DoesNotReturn]
    private static void ThrowInvalidOperationExceptionForRepeatedConfiguration()
    {
        throw new InvalidOperationException("The default service provider has already been configured.");
    }
}
