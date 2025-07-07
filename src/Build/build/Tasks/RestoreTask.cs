using Cake.Common;
using Cake.Common.IO;
using Cake.Common.Tools.DotNet;
using Cake.Core.Diagnostics;
using Cake.Core.IO;
using Cake.Frosting;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Build.Tasks;

[TaskName("Restore")]
[IsDependentOn(typeof(CleanTask))]
[TaskDescription("Restores the NuGet packages for the solution and checks for known vulnerabilities in dependencies.")]
public sealed class RestoreTask : FrostingTask<BuildContext>
{
    public override void Run(BuildContext context)
    {
        context.Log.Information("Restoring NuGet packages for the solution...");
        context.DotNetRestore(context.SourceDirectory);

        // Find all projects except the Build project.
        IEnumerable<FilePath> projectFiles = context.GetFiles($"{context.SourceDirectory}/**/*.csproj")
            .Where(f => !f.GetFilenameWithoutExtension().Equals("Build"));

        foreach (var projectFile in projectFiles)
        {
            context.Log.Information($"{Environment.NewLine}Checking {projectFile.GetFilenameWithoutExtension()} for vulnerabilities...");
            context.StartProcess("dotnet", $"list \"{projectFile.FullPath}\" package --vulnerable");
        }
    }
}
