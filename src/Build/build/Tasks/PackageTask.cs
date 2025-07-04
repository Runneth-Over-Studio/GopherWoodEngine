using Cake.Common.IO;
using Cake.Common.Tools.DotNet;
using Cake.Common.Tools.DotNet.Pack;
using Cake.Frosting;
using static Build.BuildContext;

namespace Build.Tasks;

[TaskName("Package")]
[IsDependentOn(typeof(TestsTask))]
public sealed class PackageTask : FrostingTask<BuildContext>
{
    public override bool ShouldRun(BuildContext context)
    {
        return context.Config == BuildConfigurations.Release;
    }

    public override void Run(BuildContext context)
    {
        string engineProjectPath = context.RuntimeDirectory + context.File($"{context.PublishedProjectName}.csproj");
        
        //TODO: Readme, icon, etc.

        context.DotNetPack(engineProjectPath, new DotNetPackSettings
        {
            Configuration = context.Config.ToString(),
            NoRestore = true,
            NoBuild = true,
            OutputDirectory = context.RuntimeOutputDirectory + context.Directory("NuGet")
        });
    }
}
