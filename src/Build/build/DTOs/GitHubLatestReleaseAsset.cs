using System;

namespace Build.DTOs;

public class GitHubLatestReleaseAsset
{
    public string? Url { get; set; }
    public int Id { get; set; }
    public string? Node_Id { get; set; }
    public string? Name { get; set; }
    public string? Label { get; set; }
    public string? Content_Type { get; set; }
    public string? State { get; set; }
    public int Size { get; set; }
    public object? Digest { get; set; }
    public int Download_Count { get; set; }
    public DateTime Created_At { get; set; }
    public DateTime Updated_At { get; set; }
    public string? Browser_Download_Url { get; set; }
}
