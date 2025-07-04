using Build.DTOs;
using Cake.Common;
using Cake.Common.IO;
using Cake.Core.Diagnostics;
using Cake.Core.IO;
using Cake.Frosting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Build.Tasks;

[TaskName("Documentation")]
[IsDependentOn(typeof(PackageTask))]
public sealed class DocumentationTask : AsyncFrostingTask<BuildContext>
{
    private const string DOCFX_RELEASE_URL = "https://api.github.com/repos/dotnet/docfx/releases/latest";

    public override bool ShouldRun(BuildContext context)
    {
        //return context.Config == BuildConfigurations.Release;
        return true;
    }

    public override async Task RunAsync(BuildContext context)
    {
        string docfxExePath = await VerifyDocfxToolAsync(context);

        Stopwatch stopwatch = Stopwatch.StartNew();

        // Generate the initial default docfx.json file.
        string workspace = $"../../{context.PublishedProjectName}/bin/{context.Config}/{context.TargetFramework}/Docs"; // Starts crawling from the docfx tool.
        context.StartProcess(docfxExePath, new ProcessSettings { Arguments = $"init -y -o {workspace}" });

        // Read and update the docfx.json file with the correct paths and files.
        string contextToConfigPath = context.RuntimeOutputDirectory + context.Directory("Docs/docfx.json");
        await using FileStream readStream = File.OpenRead(contextToConfigPath);
        DocfxRoot? docfxRoot = await JsonSerializer.DeserializeAsync<DocfxRoot>(readStream, context.SerializerOptions);
        DocfxSrc? docfxSrc = docfxRoot?.Metadata?.FirstOrDefault()?.Src?.FirstOrDefault();
        if (docfxSrc == null)
        {
            context.Log.Error("Failed to read & update default docfx.json file.");
            return;
        }
        readStream.Dispose();

        docfxSrc.Src = "../"; // Glob patterns in docfx currently does not support crawling files outside the directory containing docfx.json. Use the metadata.src.src property.
        docfxSrc.Files = [$"{context.PublishedProjectName}.dll"]; // When the file extension is .dll or .exe, docfx produces API docs by reflecting the assembly and the side-by-side XML documentation file.

        // Overwrite docfx.json file with the updated values.
        await using (FileStream writeStream = File.Create(contextToConfigPath))
        {
            await JsonSerializer.SerializeAsync(writeStream, docfxRoot, context.SerializerOptions);
        }

        // Generate documentation HTML.
        string workspaceToConfigPath = $"{workspace}/docfx.json";
        context.StartProcess(docfxExePath, new ProcessSettings { Arguments = $"metadata {workspaceToConfigPath}" });
        context.StartProcess(docfxExePath, new ProcessSettings { Arguments = $"build {workspaceToConfigPath}" });

        stopwatch.Stop();
        double completionTime = Math.Round(stopwatch.Elapsed.TotalSeconds, 1);
        context.Log.Information($"Documentation generation complete ({completionTime}s)");
    }

    private static async Task<string> VerifyDocfxToolAsync(BuildContext context)
    {
        (string platform, string extension) = GetDocFXPlatformAndExtension();
        string docfxExe = $"./tools/docfx/docfx{extension}";

        if (context.FileExists(docfxExe))
        {
            return docfxExe;
        }

        try
        {
            Stopwatch stopwatch = Stopwatch.StartNew();

            context.Log.Warning("docfx tool not found. Attempting to download and unzip...");

            // Get latest release info from GitHub API.
            using HttpClient httpClient = new();
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("CakeBuildScript/1.0");
            string json = await httpClient.GetStringAsync(DOCFX_RELEASE_URL);

            if (!string.IsNullOrEmpty(json))
            {
                using MemoryStream jsonStream = new(Encoding.UTF8.GetBytes(json));
                GitHubLatestReleaseRoot? root = await JsonSerializer.DeserializeAsync<GitHubLatestReleaseRoot>(jsonStream, context.SerializerOptions);

                if (root != null)
                {
                    string? downloadUrl = root.Assets?
                        .FirstOrDefault(a => a.Name != null && a.Name.EndsWith(".zip") && a.Name.StartsWith($"docfx-{platform}-x64-v"))?.Browser_Download_Url;

                    if (downloadUrl != null)
                    {
                        // Ensure the tools directory exists before creating the file.
                        Directory.CreateDirectory("./tools");

                        // Download and unzip.
                        using (var response = await httpClient.GetAsync(downloadUrl))
                        {
                            response.EnsureSuccessStatusCode();
                            using var fs = File.Create("./tools/docfx.zip");
                            await response.Content.CopyToAsync(fs);
                        }

                        context.Unzip("./tools/docfx.zip", "./tools/docfx");
                    }
                }
            }

            stopwatch.Stop();
            double completionTime = Math.Round(stopwatch.Elapsed.TotalSeconds, 1);
            context.Log.Information($"Download and unzip of docfx tool complete ({completionTime}s)");
        }
        catch (Exception ex)
        {
            context.Log.Error("Failed to download and unzip docfx tool: {0}", ex);
        }

        return docfxExe;
    }

    private static (string, string) GetDocFXPlatformAndExtension()
    {
        string platform = "linux";
        string extension = string.Empty;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            platform = "win";
            extension = ".exe";
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            platform = "osx";
        }

        return (platform, extension);
    }
}
