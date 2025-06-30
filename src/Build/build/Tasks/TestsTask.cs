using Cake.Common.Tools.DotNet;
using Cake.Common.Tools.DotNet.Test;
using Cake.Frosting;

namespace Build.Tasks;

[TaskName("Tests")]
[IsDependentOn(typeof(CompileProjectsTask))]
public sealed class TestsTask : FrostingTask<BuildContext>
{
    public override void Run(BuildContext context)
    {
        string testsProjectPath = $"{context.SourceDirectory}/Tests/Tests.csproj";

        context.DotNetTest(testsProjectPath, new DotNetTestSettings
        {
            Configuration = context.Config.ToString(),
            NoRestore = true,
            NoBuild = true
        });
    }
}
