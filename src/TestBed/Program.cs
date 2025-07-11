using GopherWoodEngine.Runtime;
using Spectre.Console;
using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace TestBed;

internal class Program
{
    private static void Main()
    {
        int exitCode = 0;
        AnsiConsole.Clear();

        try
        {
            AnsiConsole.Write(CreateTitleFiglet("Gopher Wood Engine Test Bed", "ansi-shadow.flf", new Color(130, 111, 102)));

            EngineConfig engineConfig = new()
            {
                Title = "Gopher Wood Engine Test Bed",
                Width = 1280,
                Height = 720
            };

            using TestBedGame game = new(engineConfig);
            game.Start();
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLineInterpolated($"[red]An unexpected error occurred.[/] {ex}");
            AnsiConsole.WriteLine(ex.StackTrace ?? string.Empty);
            exitCode = 1;
        }
        finally
        {
            Environment.Exit(exitCode);
        }
    }

    private static FigletText CreateTitleFiglet(string appTitle, string? fontName = null, Color? color = null)
    {
        FigletFont figFont = FigletFont.Default;

        if (!string.IsNullOrEmpty(fontName))
        {
            string? font = ReadAssemblyResource(Assembly.GetExecutingAssembly(), fontName);
            if (font != null)
            {
                figFont = FigletFont.Parse(font);
            }
        }

        return new FigletText(figFont, appTitle).Color(color ?? Color.Default);
    }

    private static string? ReadAssemblyResource(Assembly assembly, string name)
    {
        string resourcePath = assembly.GetManifestResourceNames().Single(str => str.EndsWith($".{name}"));

        using Stream? stream = assembly.GetManifestResourceStream(resourcePath);
        if (stream != null)
        {
            using StreamReader reader = new(stream);
            return reader.ReadToEnd();
        }

        return null;
    }
}
