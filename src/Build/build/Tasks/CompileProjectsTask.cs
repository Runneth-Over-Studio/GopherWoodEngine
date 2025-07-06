using Cake.Common.IO;
using Cake.Common.Tools.DotNet;
using Cake.Common.Tools.DotNet.Build;
using Cake.Core.IO;
using Cake.Frosting;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Build.Tasks;

[TaskName("Compile Projects")]
[IsDependentOn(typeof(ProcessImagesTask))]
[IsDependentOn(typeof(CompileShadersTask))]
public sealed class CompileProjectsTask : FrostingTask<BuildContext>
{
    public override void Run(BuildContext context)
    {
        // Find all .csproj files under src, excluding this Build project.
        IEnumerable<FilePath> projectPaths = context.GetFiles($"{context.SourceDirectory}/**/*.csproj")
            .Where(f => !f.FullPath.Contains("/Build/", StringComparison.OrdinalIgnoreCase));

        foreach (FilePath projectPath in projectPaths)
        {
            context.DotNetBuild(projectPath.FullPath, new DotNetBuildSettings
            {
                Configuration = context.Config.ToString(),
                NoRestore = true
            });
        }
    }
}
