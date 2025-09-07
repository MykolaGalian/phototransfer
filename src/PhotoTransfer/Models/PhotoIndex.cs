namespace PhotoTransfer.Models;

public class PhotoIndex
{
    public DateTime IndexedAt { get; set; }
    public string WorkingDirectory { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public int TotalCount { get; set; }
    public string[] SupportedExtensions { get; set; } = Array.Empty<string>();
    public List<PhotoMetadata> Photos { get; set; } = new();
}