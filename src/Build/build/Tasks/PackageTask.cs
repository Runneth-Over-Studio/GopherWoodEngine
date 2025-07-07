using Cake.Common.IO;
using Cake.Common.Tools.DotNet;
using Cake.Common.Tools.DotNet.Pack;
using Cake.Core.IO;
using Cake.Frosting;

namespace Build.Tasks;

[TaskName("Package")]
[IsDependentOn(typeof(CompileProjectsTask))]
[TaskDescription("Generates the NuGet package for the runtime using previously processed images and project properties.")]
public sealed class PackageTask : FrostingTask<BuildContext>
{
    public override bool ShouldRun(BuildContext context)
    {
        //return context.Config == BuildConfigurations.Release;
        return true;
    }

    public override void Run(BuildContext context)
    {
        string engineProjectPath = context.RuntimeDirectory + context.File($"{context.PublishedProjectName}.csproj");
        DirectoryPath nugetOutputDirectoryPath = context.RuntimeOutputDirectory + context.Directory("NuGet");

        context.DotNetPack(engineProjectPath, new DotNetPackSettings
        {
            Configuration = context.Config.ToString(),
            NoRestore = true,
            NoBuild = true,
            OutputDirectory = nugetOutputDirectoryPath
        });
    }
}
