namespace PhotoTransfer.Models;

public enum TransferType
{
    Move,
    Copy
}

public enum OperationStatus
{
    Pending,
    InProgress,
    Completed,
    Failed
}

public class TransferOperation
{
    public PhotoMetadata Photo { get; set; } = new();
    public string TargetPath { get; set; } = string.Empty;
    public TransferType Type { get; set; }
    public OperationStatus Status { get; set; }
    public string? ErrorMessage { get; set; }

    public TransferOperation()
    {
        Status = OperationStatus.Pending;
    }

    public TransferOperation(PhotoMetadata photo, string targetPath, TransferType type)
    {
        Photo = photo;
        TargetPath = targetPath;
        Type = type;
        Status = OperationStatus.Pending;
    }
}