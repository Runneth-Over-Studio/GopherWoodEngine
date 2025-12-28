using Build.Tasks.Standard;
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
        string testExecutable = System.IO.Path.Combine(context.TestsProject.OutputDirectoryPathAbsolute, context.TestsProject.Name, ".dll");

        // Run the test executable directly using dotnet exec.
        // If a solution-level global.json is ever added, can use Cake's DotNetTest. ref: https://github.com/cake-build/cake/issues/4627
        context.StartProcess("dotnet", $"exec \"{testExecutable}\"");
    }
}
