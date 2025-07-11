using GopherWoodEngine.Runtime.Modules;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Sinks.SystemConsole.Themes;
using System;
using System.Diagnostics;

namespace GopherWoodEngine.Runtime.Core;

internal static class EngineBuilder
{
    public static IServiceProvider Build(EngineConfig engineConfig)
    {
        IServiceCollection services = new ServiceCollection();

        // Core Systems
        services.AddDebugLogging();
        services.AddReleaseLogging();

        // Gameplay Foundations
        services.AddSingleton<IEventSystem, EventSystem>();

        // Low-Level Renderer
        services.AddSingleton<IGraphicsDevice>(sp => ActivatorUtilities.CreateInstance<VulkanGraphicsDevice>(sp, engineConfig));

        // Human Interface Device
        services.AddSingleton<IPhysicalDeviceIO, PhysicalDeviceIO>();

        return services.BuildServiceProvider();
    }

    [Conditional("DEBUG")]
    private static void AddDebugLogging(this IServiceCollection services)
    {
        Serilog.Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.Console(theme: AnsiConsoleTheme.Sixteen)
            .CreateLogger();

        services.AddLogging(configure => configure.AddSerilog(Serilog.Log.Logger));
    }

    [Conditional("RELEASE")]
    private static void AddReleaseLogging(this IServiceCollection services)
    {
        services.AddLogging(); // No providers, but still registers ILogger<T>
    }
}
