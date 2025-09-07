namespace PhotoTransfer.Models;

public class BaseIndex
{
    public DateTime CreatedAt { get; set; }
    public string WorkingDirectory { get; set; } = string.Empty;
    public int TotalFiles { get; set; }
    public List<string> FilePaths { get; set; } = new();
}