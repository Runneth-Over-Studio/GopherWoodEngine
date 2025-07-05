using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Build.DTOs;

public class DocfxRoot
{
    [JsonPropertyName("$schema")]
    public string? Schema { get; set; }
    public List<DocfxMetadata>? Metadata { get; set; }
    public DocfxBuild? Build { get; set; }
}

public class DocfxMetadata
{
    public List<DocfxSrc>? Src { get; set; }
    public string? Dest { get; set; }
}

public class DocfxSrc
{
    public string? Src { get; set; }
    public List<string>? Files { get; set; }
}

public class DocfxResource
{
    public List<string>? Files { get; set; }
}

public class DocfxBuild
{
    public List<DocfxContent>? Content { get; set; }
    public List<DocfxResource>? Resource { get; set; }
    public string? Output { get; set; }
    public List<string>? Template { get; set; }
    public DocfxGlobalMetadata? GlobalMetadata { get; set; }
}

public class DocfxContent
{
    public List<string>? Files { get; set; }
    public List<string>? Exclude { get; set; }
}

public class DocfxGlobalMetadata
{
    [JsonPropertyName("_appName")]
    public string? AppName { get; set; }
    [JsonPropertyName("_appTitle")]
    public string? AppTitle { get; set; }
    [JsonPropertyName("_enableSearch")]
    public bool EnableSearch { get; set; }
    public bool Pdf { get; set; }
}
