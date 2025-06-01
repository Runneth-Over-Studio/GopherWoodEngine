using System;

internal class Program
{
    private static void Main()
    {
        int exitCode = 0;

        try
        {
            //TODO:
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
