using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Sinks.SystemConsole.Themes;
using System.Diagnostics;

namespace GopherWoodEngine.Runtime.Modules;

internal static class LoggingService
{
    /// <summary>
    /// Adds Gopher Wood Engine logging services to the specified <see cref="IServiceCollection" />.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection" /> to add services to.</param>
    /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
    public static IServiceCollection AddEngineLogging(this IServiceCollection services)
    {
        services.AddDebugLogging();
        services.AddReleaseLogging();

        return services;
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
        // No providers or enabled log-levels (zero performance overhead from logging infrastructure), but still registers ILogger<T>
        services.AddLogging(configure => configure.SetMinimumLevel(LogLevel.None));
    }
}
