namespace PhotoTransfer.Models;

public class PhotoMetadata
{
    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string Extension { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string Hash { get; set; } = string.Empty;
    public DateTime CreationDate { get; set; }
    public DateTime ModificationDate { get; set; }
    public DateTime EffectiveDate { get; set; }
    public bool IsTransferred { get; set; }
    public string? TransferredTo { get; set; }
}