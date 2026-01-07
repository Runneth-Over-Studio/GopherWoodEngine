using Microsoft.Extensions.DependencyInjection;

namespace GopherWoodEngine.Runtime.Modules;

internal static class ModuleStartUp
{
    public static IServiceCollection AddEngineServices(this IServiceCollection services, EngineConfig engineConfig)
    {
        // Platform Independence
        services.AddWindowing(engineConfig);

        // Core Systems
        services.AddEngineLogging();
        services.AddSingleton<IRandomNumberGenerator>(new RandomNumberGenerator(seed: engineConfig.RandomSeed));

        // Gameplay Foundations
        services.AddSingleton<IEventSystem, EventSystem>();

        // Low-Level Renderer
        services.AddSingleton<IGraphicsDeviceInterface>(sp => ActivatorUtilities.CreateInstance<VulkanGraphicsDeviceInterface>(sp, engineConfig));

        // Human Interface Device
        services.AddSingleton<IPhysicalDeviceIO, PhysicalDeviceIO>();

        // Audio
        services.AddSingleton<IWavePlayer, OpenALWavePlayer>();

        return services;
    }
}
