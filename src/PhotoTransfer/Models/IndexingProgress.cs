namespace PhotoTransfer.Models;

public class IndexingProgress
{
    public string WorkingDirectory { get; set; } = string.Empty;
    public List<string> WorkingDirectories { get; set; } = new();
    public DateTime StartedAt { get; set; }
    public DateTime LastSavedAt { get; set; }
    public int TotalFiles { get; set; }
    public int ProcessedFiles { get; set; }
    public List<string> AllFilePaths { get; set; } = new();
    public HashSet<string> ProcessedFilePaths { get; set; } = new();
    public string CurrentOutputFile { get; set; } = string.Empty;
}