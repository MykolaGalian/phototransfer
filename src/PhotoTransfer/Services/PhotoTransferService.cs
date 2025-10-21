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

        // Group photos by camera model
        var cameraGroups = photos
            .GroupBy(photo => string.IsNullOrEmpty(photo.CameraModel) ? "Unknown" : photo.CameraModel)
            .ToDictionary(
                group => group.Key,
                group => group.ToList()
            );

        // Process each camera group
        foreach (var cameraGroup in cameraGroups)
        {
            var cameraModel = cameraGroup.Key;
            var groupPhotos = cameraGroup.Value;

            foreach (var photo in groupPhotos)
            {
                var existingOperation = FindDuplicateByName(operations, photo.FileName);

                if (existingOperation != null)
                {
                    // If current photo is larger, replace the existing operation
                    if (photo.FileSize > existingOperation.Photo.FileSize)
                    {
                        operations.Remove(existingOperation);
                        var targetPath = GenerateTargetPath(targetDirectory, photo);
                        var operation = new TransferOperation(photo, targetPath, transferType);
                        operations.Add(operation);
                    }
                    // Otherwise, skip this photo (keep the larger one)
                }
                else
                {
                    var targetPath = GenerateTargetPath(targetDirectory, photo);

                    if (File.Exists(targetPath))
                    {
                        var existingFileInfo = new FileInfo(targetPath);
                        if (photo.FileSize <= existingFileInfo.Length)
                        {
                            // Skip this photo as existing file is equal or larger
                            continue;
                        }
                        // Current photo is larger, so we'll overwrite
                    }

                    var operation = new TransferOperation(photo, targetPath, transferType);
                    operations.Add(operation);
                }
            }
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
                    File.Copy(operation.Photo.FilePath, operation.TargetPath, overwrite: true);
                }
                else
                {
                    // For move operations, handle existing files by deleting them first
                    if (File.Exists(operation.TargetPath))
                    {
                        File.Delete(operation.TargetPath);
                    }
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

    private TransferOperation? FindDuplicateByName(List<TransferOperation> operations, string fileName)
    {
        return operations.FirstOrDefault(op => 
            Path.GetFileName(op.TargetPath).Equals(fileName, StringComparison.OrdinalIgnoreCase));
    }

    private string GenerateTargetPath(string targetDirectory, PhotoMetadata photo)
    {
        // Determine camera model directory name
        var cameraModel = string.IsNullOrEmpty(photo.CameraModel) ? "Unknown" : photo.CameraModel;

        // Create base camera model directory
        var cameraModelDirectory = Path.Combine(targetDirectory, cameraModel);

        // If photo has source directory, create subdirectory inside camera model directory
        if (!string.IsNullOrEmpty(photo.SourceDirectory))
        {
            var sourceSubdirectory = Path.Combine(cameraModelDirectory, photo.SourceDirectory);
            return Path.Combine(sourceSubdirectory, photo.FileName);
        }

        // No source directory, place file directly in camera model directory
        return Path.Combine(cameraModelDirectory, photo.FileName);
    }
}