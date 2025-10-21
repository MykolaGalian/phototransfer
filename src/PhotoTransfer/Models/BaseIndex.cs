namespace PhotoTransfer.Models;

public class BaseIndex
{
    public DateTime CreatedAt { get; set; }
    public string WorkingDirectory { get; set; } = string.Empty;
    public List<string> WorkingDirectories { get; set; } = new();
    public int TotalFiles { get; set; }
    public List<string> FilePaths { get; set; } = new();
}