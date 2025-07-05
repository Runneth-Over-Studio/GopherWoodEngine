using Build.DTOs;
using Cake.Common;
using Cake.Common.IO;
using Cake.Core.Diagnostics;
using Cake.Core.IO;
using Cake.Frosting;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace Build.Tasks;

[TaskName("Documentation")]
[IsDependentOn(typeof(PackageTask))]
public sealed class DocumentationTask : AsyncFrostingTask<BuildContext>
{
    public override bool ShouldRun(BuildContext context)
    {
        //return context.Config == BuildConfigurations.Release;
        return true;
    }

    public override async Task RunAsync(BuildContext context)
    {
        if (!VerifyDocfxTool(context))
        {
            context.Log.Error("Aborting documentation generation.");
            return;
        }

        Stopwatch stopwatch = Stopwatch.StartNew();

        DirectoryPath workspaceDirectoryPath = context.RuntimeOutputDirectory + context.Directory("Docs");
        string workspaceFullPath = workspaceDirectoryPath.FullPath;

        // Generate the initial default docfx.json file.
        context.StartProcess("docfx", new ProcessSettings { Arguments = $"init -y -o {workspaceFullPath}" });

        // Read the default docfx.json
        string contextToConfigPath = context.RuntimeOutputDirectory + context.Directory("Docs/docfx.json");
        await using FileStream readStream = File.OpenRead(contextToConfigPath);
        DocfxRoot? docfxRoot = await JsonSerializer.DeserializeAsync<DocfxRoot>(readStream, context.SerializerOptions);
        DocfxSrc? docfxSrc = docfxRoot?.Metadata?.FirstOrDefault()?.Src?.FirstOrDefault();
        DocfxGlobalMetadata? globalMetadata = docfxRoot?.Build?.GlobalMetadata;
        if (docfxSrc == null || globalMetadata == null)
        {
            context.Log.Error("Failed to read & update default docfx.json file.");
            return;
        }
        readStream.Dispose();

        // Update the docfx config.
        docfxSrc.Src = "../"; // Glob patterns in docfx currently does not support crawling files outside the directory containing docfx.json. Use the metadata.src.src property.
        docfxSrc.Files = [$"{context.PublishedProjectName}.dll"]; // When the file extension is .dll or .exe, docfx produces API docs by reflecting the assembly and the side-by-side XML documentation file.
        globalMetadata.AppTitle = "Gopher Wood Engine"; // Used in the generated HTML title tag.
        globalMetadata.AppName = "Gopher Wood Engine"; // Used in the generated HTML header.

        //TODO: Need to further tweak the docfx source files to make the resulting html docs our own.
        //      ref: https://dotnet.github.io/docfx/docs/basic-concepts.html
        //      ref: https://dotnet.github.io/docfx/reference/docfx-json-reference.html
        //      ref: https://dotnet.github.io/docfx/reference/docfx-json-reference.html#predefined-metadata
        //      ref: https://code-maze.com/docfx-generating-source-code-documentation/
        //      ref: https://youtu.be/Sz1lCeedcPI?si=I0YHUhgI0ZKjO2cq

        // Overwrite docfx.json file with the updated values.
        await using (FileStream writeStream = File.Create(contextToConfigPath))
        {
            await JsonSerializer.SerializeAsync(writeStream, docfxRoot, context.SerializerOptions);
        }

        // Generate documentation HTML.
        string workspaceToConfigPath = System.IO.Path.Combine(workspaceFullPath, "docfx.json");
        context.StartProcess("docfx", new ProcessSettings { Arguments = $"metadata {workspaceToConfigPath}" });
        context.StartProcess("docfx", new ProcessSettings { Arguments = $"build {workspaceToConfigPath}" });

        stopwatch.Stop();
        double completionTime = Math.Round(stopwatch.Elapsed.TotalSeconds, 1);
        context.Log.Information($"Documentation generation complete ({completionTime}s)");
    }

    private static bool VerifyDocfxTool(BuildContext context)
    {
        try
        {
            Stopwatch stopwatch = Stopwatch.StartNew();

            context.Log.Information("Verifying global dotnet docfx tool is installed/updated...");
            int exitCode = context.StartProcess("dotnet", new ProcessSettings { Arguments = "tool update -g docfx" });

            if (exitCode != 0)
            {
                context.Log.Error("Failed to update or install docfx tool. Exit code: {0}", exitCode);
                return false;
            }

            stopwatch.Stop();
            double completionTime = Math.Round(stopwatch.Elapsed.TotalSeconds, 1);
            context.Log.Information($"Verification of docfx tool complete ({completionTime}s)");
        }
        catch (Exception ex)
        {
            context.Log.Error("Failed to update or install docfx tool: {0}", ex);
            return false;
        }

        return true;
    }
}
