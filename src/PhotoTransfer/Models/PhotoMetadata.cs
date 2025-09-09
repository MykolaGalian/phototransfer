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
    
    // All collected metadata dates for analysis and debugging
    public List<DateSource> AllDates { get; set; } = new List<DateSource>();
    
    public bool IsTransferred { get; set; }
    public string? TransferredTo { get; set; }
}

public class DateSource
{
    public DateTime Date { get; set; }
    public string Source { get; set; } = string.Empty; // "FileSystem.Creation", "EXIF.DateTimeOriginal", etc.
    public bool IsPlaceholder { get; set; } // Indicates if this is likely a placeholder date
}