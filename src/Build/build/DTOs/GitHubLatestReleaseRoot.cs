using System;
using System.Collections.Generic;

namespace Build.DTOs;

public class GitHubLatestReleaseRoot
{
    public string? Url { get; set; }
    public string? Assets_Url { get; set; }
    public string? Upload_Url { get; set; }
    public string? Html_Url { get; set; }
    public int Id { get; set; }
    public string? Node_Id { get; set; }
    public string? Tag_Name { get; set; }
    public string? Target_Commitish { get; set; }
    public string? Name { get; set; }
    public bool Draft { get; set; }
    public bool Prerelease { get; set; }
    public DateTime Created_At { get; set; }
    public DateTime Published_At { get; set; }
    public List<GitHubLatestReleaseAsset>? Assets { get; set; }
    public string? Tarball_Url { get; set; }
    public string? Zipball_Url { get; set; }
    public string? Body { get; set; }
    public int Mentions_Count { get; set; }
}
