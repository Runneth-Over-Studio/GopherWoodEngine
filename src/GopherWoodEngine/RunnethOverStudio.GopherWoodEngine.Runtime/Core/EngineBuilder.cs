using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RunnethOverStudio.GopherWoodEngine.Runtime.Modules;
using System;
using System.Diagnostics;

namespace RunnethOverStudio.GopherWoodEngine.Runtime.Core;

internal static class EngineBuilder
{
    public static IServiceProvider Build(EngineConfig engineConfig)
    {
        IServiceCollection services = new ServiceCollection();

        services.AddDebugLogging();

        services
            .AddSingleton<IGraphicsDevice>(sp => ActivatorUtilities.CreateInstance<VulkanGraphicsDevice>(sp, engineConfig));

        return services.BuildServiceProvider();
    }

    [Conditional("DEBUG")]
    private static void AddDebugLogging(this IServiceCollection services)
    {
        services.AddLogging(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Trace);
            builder.AddSimpleConsole(options =>
            {
                options.IncludeScopes = true;
                options.SingleLine = true;
                options.TimestampFormat = "HH:mm:ss ";
            });
        });
    }
}
