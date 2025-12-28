using Build.DTOs;
using Cake.Common.Tools.DotNet;
using Cake.Common.Tools.DotNet.Build;
using Cake.Common.Tools.MSBuild;
using Cake.Core.Diagnostics;
using Cake.Frosting;

namespace Build.Tasks.Standard;

[TaskName("Compile Projects")]
[IsDependentOn(typeof(LintingTask))]
[IsDependentOn(typeof(ProcessImagesTask))]
[IsDependentOn(typeof(CompileShadersTask))]
[TaskDescription("Compiles all projects in the src directory, excluding the Build project.")]
public sealed class CompileProjectsTask : FrostingTask<BuildContext>
{
    public override void Run(BuildContext context)
    {
        foreach (BuildProject buildProject in context.BuildProjects)
        {
            CompileProject(context, buildProject);
        }
    }

    private static void CompileProject(BuildContext context, BuildProject buildProject)
    {
        if (buildProject.IsSdkStyleProject)
        {
            context.DotNetBuild(buildProject.CsprojFilePathAbsolute, new DotNetBuildSettings
            {
                Configuration = context.Config.ToString(),
                NoRestore = true
            });
        }
        else
        {
            context.MSBuild(buildProject.CsprojFilePathAbsolute, new MSBuildSettings
            {
                Target = "Build",
                Configuration = context.Config.ToString(),
                Verbosity = Verbosity.Minimal
            });
        }
    }
}
