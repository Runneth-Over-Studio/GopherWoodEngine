using Cake.Common.Tools.DotNet;
using Cake.Common.Tools.DotNet.Pack;
using Cake.Core.Diagnostics;
using Cake.Frosting;
using static Build.BuildContext;

namespace Build.Tasks;

[TaskName("Package")]
[IsDependentOn(typeof(TestsTask))]
public sealed class PackageTask : FrostingTask<BuildContext>
{
    public override void Run(BuildContext context)
    {
        if (context.Config != BuildConfigurations.Release)
        {
            context.Log.Information("Skipping. Use Release configuration for production builds.");
        }
        else
        {
            string engineProjectPath = $"{context.SourceDirectory}/GopherWoodEngine.Runtime/GopherWoodEngine.Runtime.csproj";

            //TODO: Readme, icon, etc.

            context.DotNetPack(engineProjectPath, new DotNetPackSettings
            {
                Configuration = context.Config.ToString(),
                NoRestore = true,
                NoBuild = true,
                OutputDirectory = context.EngineOutputDirectory
            });
        }
    }
}
