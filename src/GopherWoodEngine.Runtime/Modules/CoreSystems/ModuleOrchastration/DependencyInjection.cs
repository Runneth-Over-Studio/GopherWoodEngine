using Microsoft.Extensions.DependencyInjection;

namespace GopherWoodEngine.Runtime.Modules;

internal static class DependencyInjection
{
    public static IServiceCollection AddEngineServices(this IServiceCollection services, EngineConfig engineConfig)
    {
        // Core Systems
        services.AddEngineLogging();
        services.AddSingleton<IRandomNumberGenerator>(new RandomNumberGenerator(seed: engineConfig.RandomSeed));

        // Gameplay Foundations
        services.AddSingleton<IEventSystem, EventSystem>();

        // Low-Level Renderer
        services.AddSingleton<IVirtualScreen>(sp => ActivatorUtilities.CreateInstance<VulkanVirtualScreen>(sp, engineConfig));
        services.AddSingleton<IGraphicsDevice, VulkanGraphicsDevice>();

        // Human Interface Device
        services.AddSingleton<IPhysicalDeviceIO, PhysicalDeviceIO>();

        // Audio
        services.AddSingleton<IWavePlayer, OpenALWavePlayer>();

        return services;
    }
}
