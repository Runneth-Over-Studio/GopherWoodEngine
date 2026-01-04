using Build.Tasks.Standard;
using Cake.Common;
using Cake.Common.Diagnostics;
using Cake.Common.IO;
using Cake.Core;
using Cake.Core.Diagnostics;
using Cake.Core.IO;
using Cake.Frosting;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace Build.Tasks;

[TaskName("Compile Shaders")]
[IsDependentOn(typeof(RestoreTask))]
[TaskDescription("Compiles SPIR-V shader binaries for all .vert/.frag files under the repo.")]
public sealed class CompileShadersTask : FrostingTask<BuildContext>
{
    private const string ARGS_FORMAT = "\"{0}\" -o \"{1}\"";

    public override void Run(BuildContext context)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();

        string vulkanSdkPath = GetVulkanSDKPath(context);
        string glslcPath = System.IO.Path.Combine(vulkanSdkPath, "Bin", GetGlslcFileName());
        string sourceRoot = context.SourceDirectory.Path.FullPath;

        FilePath[] shaderFiles = [.. context.GetFiles($"{sourceRoot}/**/*.vert")
            .Concat(context.GetFiles($"{sourceRoot}/**/*.frag"))
            .Where(f => !IsUnderBuildOutput(f))
            .OrderBy(f => f.FullPath)];

        if (shaderFiles.Length == 0)
        {
            context.Log.Information("No .vert or .frag shader files found.");
            return;
        }

        int compiled = 0;
        int skipped = 0;

        foreach (FilePath file in shaderFiles)
        {
            string sourcePath = file.FullPath;
            string spirvPath = sourcePath + ".spv";

            if (IsUpToDate(sourcePath, spirvPath))
            {
                skipped++;
                continue;
            }

            int exitCode = context.StartProcess(glslcPath, new ProcessSettings { Arguments = string.Format(ARGS_FORMAT, sourcePath, spirvPath) });
            if (exitCode != 0)
            {
                throw new Exception($"glslc failed ({exitCode}) compiling '{sourcePath}'.");
            }

            compiled++;
            context.Log.Information($"Compiled: {sourcePath} -> {spirvPath}");
        }

        stopwatch.Stop();
        double completionTime = Math.Round(stopwatch.Elapsed.TotalSeconds, 1);
        context.Log.Information($"Shader compilation complete. Compiled={compiled}, Skipped={skipped} ({completionTime}s)");
    }

    private static bool IsUnderBuildOutput(FilePath file)
    {
        // Normalize separators for simple contains checks.
        string p = file.FullPath.Replace('\\', '/');

        // Skip intermediate/output folders.
        return p.Contains("/bin/", StringComparison.OrdinalIgnoreCase)
            || p.Contains("/obj/", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsUpToDate(string sourcePath, string spirvPath)
    {
        if (!File.Exists(spirvPath))
        {
            return false;
        }

        DateTime srcTime = File.GetLastWriteTimeUtc(sourcePath);
        DateTime spvTime = File.GetLastWriteTimeUtc(spirvPath);

        return spvTime >= srcTime;
    }

    private static string GetVulkanSDKPath(BuildContext context)
    {
        string vulkanSdkPath = context.EnvironmentVariable("VULKAN_SDK");

        if (string.IsNullOrEmpty(vulkanSdkPath))
        {
            throw new InvalidOperationException("VULKAN_SDK environment variable is required to be set. Please verify Vulkan SDK installation and that the environment variable is correctly set.");
        }

        return vulkanSdkPath;
    }

    private static string GetGlslcFileName()
    {
        string glslcFileName = "glslc";

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            glslcFileName += ".exe";
        }

        return glslcFileName;
    }
}
