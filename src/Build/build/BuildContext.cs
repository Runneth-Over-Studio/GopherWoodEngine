using Cake.Common.IO;
using Cake.Common.IO.Paths;
using Cake.Core;
using Cake.Frosting;

namespace Build;

public class BuildContext : FrostingContext
{
    public enum BuildConfigurations
    {
        Debug,
        Release
    }

    public BuildConfigurations Config { get; }
    public ConvertableDirectoryPath SourceDirectory { get; }
    public ConvertableDirectoryPath EngineOutputDirectory { get; }

    public BuildContext(ICakeContext context) : base(context)
    {
        SourceDirectory = context.Directory("../../../src");
        EngineOutputDirectory = SourceDirectory + context.Directory("GopherWoodEngine.Runtime/bin/output");

        string configArgument = context.Arguments.GetArgument("Configuration") ?? string.Empty;
        Config = configArgument.ToLower() switch
        {
            "release" => BuildConfigurations.Release,
            _ => BuildConfigurations.Debug,
        };
    }
}
