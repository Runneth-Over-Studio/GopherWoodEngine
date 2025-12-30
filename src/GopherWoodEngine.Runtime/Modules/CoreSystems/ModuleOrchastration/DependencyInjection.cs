using Microsoft.Extensions.DependencyInjection;

namespace GopherWoodEngine.Runtime.Modules;

internal static class DependencyInjection
{
    public static IServiceCollection AddEngineServices(this IServiceCollection services, EngineConfig engineConfig)
    {
        // Core Systems
        services.AddEngineLogging();
        services.AddKeyedSingleton<IRandomNumberGenerator, RandomNumberGenerator>("ThreadSafe", (sp, key) => new RandomNumberGenerator(seed: null));
        services.AddKeyedSingleton<IRandomNumberGenerator, RandomNumberGenerator>("Deterministic", (sp, key) => new RandomNumberGenerator(seed: engineConfig.RandomSeed ?? 0));
        services.AddSingleton(sp => sp.GetRequiredKeyedService<IRandomNumberGenerator>("ThreadSafe")); // Register default (non-keyed), resolves to ThreadSafe by default.

        // Gameplay Foundations
        services.AddSingleton<IEventSystem, EventSystem>();

        // Low-Level Renderer
        services.AddSingleton<IVirtualScreen>(sp => ActivatorUtilities.CreateInstance<VulkanVirtualScreen>(sp, engineConfig));
        services.AddSingleton<IGraphicsDevice, VulkanGraphicsDevice>();

        // Human Interface Device
        services.AddSingleton<IPhysicalDeviceIO, PhysicalDeviceIO>();

        return services;
    }
}
