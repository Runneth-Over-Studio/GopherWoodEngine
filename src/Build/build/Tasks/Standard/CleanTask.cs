using Build.DTOs;
using Cake.Common.IO;
using Cake.Core.Diagnostics;
using Cake.Core.IO;
using Cake.Frosting;

namespace Build.Tasks.Standard;

[TaskName("Clean")]
[TaskDescription("Deletes the Debug or Release directories in the project bin directories.")]
public sealed class CleanTask : FrostingTask<BuildContext>
{
    public override void Run(BuildContext context)
    {
        foreach (BuildProject buildProject in context.BuildProjects)
        {
            DirectoryPath buildDirectory = DirectoryPath.FromString(System.IO.Path.Combine(buildProject.DirectoryPathAbsolute, "bin", context.Config.ToString()));

            context.CleanDirectory(buildDirectory);
            context.Log.Information($"Cleaned {buildDirectory}");
        }
    }
}
