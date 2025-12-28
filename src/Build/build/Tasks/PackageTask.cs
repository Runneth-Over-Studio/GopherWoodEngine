using Cake.Common.IO;
using Cake.Common.Tools.DotNet;
using Cake.Common.Tools.DotNet.Pack;
using Cake.Core.IO;
using Cake.Frosting;
using static Build.BuildContext;

namespace Build.Tasks;

[TaskName("Package")]
[IsDependentOn(typeof(DocumentationTask))]
[TaskDescription("Generates the NuGet package for the runtime using previously processed images and project properties.")]
public sealed class PackageTask : FrostingTask<BuildContext>
{
    public override bool ShouldRun(BuildContext context)
    {
        return context.Config == BuildConfigurations.Release;
    }

    public override void Run(BuildContext context)
    {
        DirectoryPath outputPath = DirectoryPath.FromString(context.EngineRuntimeProject.OutputDirectoryPathAbsolute);
        DirectoryPath nugetOutputDirectoryPath = outputPath + context.Directory("NuGet");

        context.DotNetPack(context.EngineRuntimeProject.CsprojFilePathAbsolute, new DotNetPackSettings
        {
            Configuration = context.Config.ToString(),
            NoRestore = true,
            NoBuild = true,
            OutputDirectory = nugetOutputDirectoryPath
        });
    }
}
