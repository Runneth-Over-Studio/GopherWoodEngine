using ArchUnitNET.Domain;
using ArchUnitNET.Loader;

namespace Tests.Runtime;

public class ArchitectureTests
{
    private static readonly Architecture _architecture = new ArchLoader().LoadAssemblies(
            System.Reflection.Assembly.Load("GopherWoodEngine.Runtime.Core.Engine")
        ).Build();
}
