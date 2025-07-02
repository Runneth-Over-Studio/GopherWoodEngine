using Cake.Common;
using Cake.Common.IO;
using Cake.Common.IO.Paths;
using Cake.Core.Diagnostics;
using Cake.Core.IO;
using Cake.Frosting;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Build.Tasks;

[TaskName("Compile Shaders")]
[IsDependentOn(typeof(RestoreTask))]
public sealed class CompileShadersTask : FrostingTask<BuildContext>
{
    private const string ARGS_FORMAT = "\"{0}\" -o \"{1}\"";

    public override void Run(BuildContext context)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();

        string vulkanSdkPath = GetVulkanSDKPath(context);
        string glslcFileName = GetGlslcFileName(context);
        string glslcPath = System.IO.Path.Combine(vulkanSdkPath, "Bin", glslcFileName);

        ConvertableDirectoryPath shadersPath = context.EngineDirectory + context.Directory("Modules/LowLevelRenderer/Shaders");
        string vertexSourcePath = System.IO.Path.Combine(shadersPath, "shader_base.vert");
        string vertexSPIRVPath = System.IO.Path.Combine(shadersPath, "shader_base.vert.spv");
        string fragmentSourcePath = System.IO.Path.Combine(shadersPath, "shader_base.frag");
        string fragmentSPIRVPath = System.IO.Path.Combine(shadersPath, "shader_base.frag.spv");

        context.StartProcess(glslcPath, new ProcessSettings { Arguments = string.Format(ARGS_FORMAT, vertexSourcePath, vertexSPIRVPath) });
        context.StartProcess(glslcPath, new ProcessSettings { Arguments = string.Format(ARGS_FORMAT, fragmentSourcePath, fragmentSPIRVPath) });

        stopwatch.Stop();
        double completionTime = Math.Round(stopwatch.Elapsed.TotalSeconds, 1);
        context.Log.Information($"Compilation of SPIR-V shader binaries complete ({completionTime}s)");
    }

    private string GetVulkanSDKPath(BuildContext context)
    {
        string vulkanSdkPath = context.EnvironmentVariable("VULKAN_SDK");

        if (string.IsNullOrEmpty(vulkanSdkPath))
        {
            throw new InvalidOperationException("VULKAN_SDK environment variable is not set. Please install the Vulkan SDK.");
        }

        return vulkanSdkPath;
    }

    private string GetGlslcFileName(BuildContext context)
    {
        string glslcFileName = "glslc";

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            glslcFileName += ".exe";
        }

        return glslcFileName;
    }
}
