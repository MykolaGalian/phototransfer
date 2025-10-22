namespace PhotoTransfer.Models;

/// <summary>
/// Represents the current progress of a transfer operation
/// </summary>
public class TransferProgress
{
    public int TotalOperations { get; set; }
    public int CompletedOperations { get; set; }
    public int SkippedOperations { get; set; }
    public int FailedOperations { get; set; }
    public long TotalBytesTransferred { get; set; }
    public double BytesPerSecond { get; set; }
    public TimeSpan ElapsedTime { get; set; }

    public double PercentComplete => TotalOperations > 0
        ? (double)CompletedOperations / TotalOperations * 100
        : 0;

    public string FormattedSpeed => FormatBytes((long)BytesPerSecond) + "/s";

    public string FormattedTotalTransferred => FormatBytes(TotalBytesTransferred);

    public TimeSpan EstimatedTimeRemaining
    {
        get
        {
            if (BytesPerSecond <= 0 || CompletedOperations == 0)
            {
                return TimeSpan.Zero;
            }

            var remainingOperations = TotalOperations - CompletedOperations;
            if (remainingOperations <= 0)
            {
                return TimeSpan.Zero;
            }

            var avgTimePerOperation = ElapsedTime.TotalSeconds / CompletedOperations;
            return TimeSpan.FromSeconds(avgTimePerOperation * remainingOperations);
        }
    }

    private static string FormatBytes(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }
}
