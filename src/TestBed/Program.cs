using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RazorConsole.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace TestBed;

public class Program
{
    public static async Task Main(string[] args)
    {
        int exitCode = 0;

        try
        {
            HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
            builder.UseRazorConsole<App>();

            RegisterServices(builder.Services);

            IHost host = builder.Build();
            await host.RunAsync();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Application terminated unexpectedly: {ex}");
            exitCode = 1;
        }
        finally
        {
            Environment.Exit(exitCode);
        }
    }

    private static void RegisterServices(IServiceCollection services)
    {
        // Auto-register all ViewModels by convention.
        IEnumerable<Type> viewModelTypes = Assembly.GetExecutingAssembly()
            .GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && t.Name.EndsWith("ViewModel"));

        foreach (Type viewModelType in viewModelTypes)
        {
            services.AddScoped(viewModelType);
        }
    }
}
