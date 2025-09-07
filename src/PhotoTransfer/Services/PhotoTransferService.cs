using PhotoTransfer.Models;

namespace PhotoTransfer.Services;

public class PhotoTransferService
{
    private readonly MetadataStore _metadataStore;

    public PhotoTransferService(MetadataStore metadataStore)
    {
        _metadataStore = metadataStore;
    }

    public PhotoTransferService() : this(new MetadataStore())
    {
    }

    public List<PhotoMetadata> GetPhotosForPeriod(PhotoIndex index, TimePeriod period)
    {
        return index.Photos
            .Where(photo => period.Contains(photo.EffectiveDate))
            .ToList();
    }

    public List<TransferOperation> PlanTransfer(List<PhotoMetadata> photos, string targetDirectory, TransferType transferType = TransferType.Move)
    {
        var operations = new List<TransferOperation>();

        foreach (var photo in photos)
        {
            var targetPath = GenerateTargetPath(targetDirectory, photo, operations);
            var operation = new TransferOperation(photo, targetPath, transferType);
            operations.Add(operation);
        }

        return operations;
    }

    public void ExecuteTransfer(List<TransferOperation> operations, bool dryRun = false)
    {
        foreach (var operation in operations)
        {
            try
            {
                operation.Status = OperationStatus.InProgress;

                if (dryRun)
                {
                    // In dry run mode, just mark as completed without actual file operations
                    operation.Status = OperationStatus.Completed;
                    continue;
                }

                // Ensure target directory exists
                var targetDir = Path.GetDirectoryName(operation.TargetPath);
                if (!string.IsNullOrEmpty(targetDir) && !Directory.Exists(targetDir))
                {
                    Directory.CreateDirectory(targetDir);
                }

                // Verify source file still exists
                if (!File.Exists(operation.Photo.FilePath))
                {
                    throw new FileNotFoundException($"Source file no longer exists: {operation.Photo.FilePath}");
                }

                // Perform the file operation
                if (operation.Type == TransferType.Copy)
                {
                    File.Copy(operation.Photo.FilePath, operation.TargetPath, overwrite: false);
                }
                else
                {
                    File.Move(operation.Photo.FilePath, operation.TargetPath);
                }

                operation.Status = OperationStatus.Completed;
            }
            catch (Exception ex)
            {
                operation.Status = OperationStatus.Failed;
                operation.ErrorMessage = ex.Message;
            }
        }
    }

    public void UpdateMetadataAfterTransfer(string metadataFilePath, List<TransferOperation> completedOperations)
    {
        foreach (var operation in completedOperations.Where(op => op.Status == OperationStatus.Completed))
        {
            try
            {
                _metadataStore.UpdatePhotoTransferStatus(metadataFilePath, operation.Photo.Hash, operation.TargetPath);
            }
            catch
            {
                // Continue with other updates if one fails
                continue;
            }
        }
    }

    private string GenerateTargetPath(string targetDirectory, PhotoMetadata photo, List<TransferOperation> existingOperations)
    {
        var fileName = photo.FileName;
        var targetPath = Path.Combine(targetDirectory, fileName);

        // Check for conflicts with existing operations
        var conflictCount = existingOperations.Count(op => 
            Path.GetFileName(op.TargetPath).Equals(fileName, StringComparison.OrdinalIgnoreCase));

        if (conflictCount > 0)
        {
            var extension = Path.GetExtension(fileName);
            var nameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
            fileName = $"{nameWithoutExtension}({conflictCount}){extension}";
            targetPath = Path.Combine(targetDirectory, fileName);
        }

        // Check for conflicts with existing files in target directory
        while (File.Exists(targetPath))
        {
            var extension = Path.GetExtension(fileName);
            var nameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
            
            // Extract existing suffix or start with 0
            var match = System.Text.RegularExpressions.Regex.Match(nameWithoutExtension, @"^(.+)\((\d+)\)$");
            if (match.Success)
            {
                var baseName = match.Groups[1].Value;
                var currentNum = int.Parse(match.Groups[2].Value);
                fileName = $"{baseName}({currentNum + 1}){extension}";
            }
            else
            {
                fileName = $"{nameWithoutExtension}(0){extension}";
            }
            
            targetPath = Path.Combine(targetDirectory, fileName);
        }

        return targetPath;
    }
}