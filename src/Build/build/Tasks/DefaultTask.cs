using Cake.Core;
using Cake.Core.Diagnostics;
using Cake.Frosting;

namespace Build.Tasks;

// The top-level default task is the entry point for the build process when a command-line target isn't specified.

[TaskName("Default")]
[IsDependentOn(typeof(PackageTask))]
public class DefaultTask : FrostingTask
{
    public override void Run(ICakeContext context)
    {
        context.Log.Information("Set the task to run with --target [task]");
    }
}
