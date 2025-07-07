using Cake.Common.IO;
using Cake.Common.IO.Paths;
using Cake.Common.Xml;
using Cake.Core;
using Cake.Frosting;
using System.Text.Json;

namespace Build;

public sealed class BuildContext : FrostingContext
{
    public enum BuildConfigurations
    {
        Debug,
        Release
    }

    public BuildConfigurations Config { get; }
    public JsonSerializerOptions SerializerOptions { get; }
    public string TargetFramework { get; }
    public string PublishedProjectName { get; }
    public ConvertableDirectoryPath RootDirectory { get; }
    public ConvertableDirectoryPath SourceDirectory { get; }
    public ConvertableDirectoryPath RuntimeDirectory { get; }
    public ConvertableDirectoryPath RuntimeOutputDirectory { get; }


    public BuildContext(ICakeContext context) : base(context)
    {
        SerializerOptions = new() { PropertyNameCaseInsensitive = true };
        PublishedProjectName = "GopherWoodEngine.Runtime";
        RootDirectory = context.Directory("../../../");
        SourceDirectory = RootDirectory + context.Directory("src");
        RuntimeDirectory = SourceDirectory + context.Directory(PublishedProjectName);
        TargetFramework = context.XmlPeek(RuntimeDirectory + context.File($"{PublishedProjectName}.csproj"), "/Project/PropertyGroup/TargetFramework");

        string configArgument = context.Arguments.GetArgument("Configuration") ?? string.Empty;
        Config = configArgument.ToLower() switch
        {
            "release" => BuildConfigurations.Release,
            _ => BuildConfigurations.Debug,
        };

        RuntimeOutputDirectory = RuntimeDirectory + context.Directory($"bin/{Config}/{TargetFramework}");
    }
}
