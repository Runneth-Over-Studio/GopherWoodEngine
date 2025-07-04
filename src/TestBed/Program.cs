﻿using GopherWoodEngine.Runtime;
using System;

namespace TestBed;

internal class Program
{
    private static void Main()
    {
        int exitCode = 0;
        Console.Clear();

        try
        {
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
            Console.WriteLine("An unexpected error occurred: {0}", ex);
            Console.WriteLine(ex.StackTrace);
            exitCode = 1;
        }
        finally
        {
            Environment.Exit(exitCode);
        }
    }
}
