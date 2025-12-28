using Cake.Common;
using Cake.Frosting;

namespace Build.Tasks;

[TaskName("Tests")]
[IsDependentOn(typeof(CompileProjectsTask))]
[TaskDescription("Runs all Tests-project tests.")]
public sealed class TestsTask : FrostingTask<BuildContext>
{
    public override void Run(BuildContext context)
    {
        string testExecutable = $"{context.SourceDirectory}/Tests/bin/{context.Config}/net10.0/Tests.dll";

        // Run the test executable directly using dotnet exec
        context.StartProcess("dotnet", $"exec \"{testExecutable}\"");
    }
}
