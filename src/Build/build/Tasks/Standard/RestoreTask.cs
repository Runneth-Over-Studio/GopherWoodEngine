using Build.DTOs;
using Cake.Common;
using Cake.Common.Tools.DotNet;
using Cake.Common.Tools.MSBuild;
using Cake.Core.Diagnostics;
using Cake.Frosting;
using System;

namespace Build.Tasks.Standard;

[TaskName("Restore")]
[IsDependentOn(typeof(CleanTask))]
[TaskDescription("Restores the NuGet packages for the solution and checks for known vulnerabilities in dependencies.")]
public sealed class RestoreTask : FrostingTask<BuildContext>
{
    public override void Run(BuildContext context)
    {
        context.Log.Information("Restoring NuGet packages for the solution...");

        foreach (BuildProject buildProject in context.BuildProjects)
        {
            RestoreProject(context, buildProject);
        }
    }

    private static void RestoreProject(BuildContext context, BuildProject buildProject)
    {
        if (buildProject.IsSdkStyleProject)
        {
            context.DotNetRestore(buildProject.CsprojFilePathAbsolute);
            context.Log.Information($"{Environment.NewLine}Checking {buildProject.Name} for vulnerabilities...");
            context.StartProcess("dotnet", $"list \"{buildProject.CsprojFilePathAbsolute}\" package --vulnerable");
        }
        else
        {
            // For legacy projects, use MS Build restore.
            context.MSBuild(buildProject.CsprojFilePathAbsolute, new MSBuildSettings
            {
                Target = "Restore"
            });
        }
    }
}
