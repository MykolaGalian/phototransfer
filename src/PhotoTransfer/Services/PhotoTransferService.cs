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

        // First, group photos by camera model and collect unique source directories for each camera
        var cameraGroups = photos
            .GroupBy(photo => string.IsNullOrEmpty(photo.CameraModel) ? "Unknown" : photo.CameraModel)
            .ToDictionary(
                group => group.Key,
                group => new
                {
                    Photos = group.ToList(),
                    SourceDirectories = group
                        .Where(p => !string.IsNullOrEmpty(p.SourceDirectory))
                        .Select(p => p.SourceDirectory)
                        .Distinct()
                        .OrderBy(d => d)
                        .ToList()
                }
            );

        // Process each camera group
        foreach (var cameraGroup in cameraGroups)
        {
            var cameraModel = cameraGroup.Key;
            var groupPhotos = cameraGroup.Value.Photos;
            var sourceDirectories = cameraGroup.Value.SourceDirectories;

            foreach (var photo in groupPhotos)
            {
                var existingOperation = FindDuplicateByName(operations, photo.FileName);

                if (existingOperation != null)
                {
                    // If current photo is larger, replace the existing operation
                    if (photo.FileSize > existingOperation.Photo.FileSize)
                    {
                        operations.Remove(existingOperation);
                        var targetPath = GenerateTargetPath(targetDirectory, photo, sourceDirectories);
                        var operation = new TransferOperation(photo, targetPath, transferType);
                        operations.Add(operation);
                    }
                    // Otherwise, skip this photo (keep the larger one)
                }
                else
                {
                    var targetPath = GenerateTargetPath(targetDirectory, photo, sourceDirectories);

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

    private string GenerateTargetPath(string targetDirectory, PhotoMetadata photo, List<string> sourceDirectories)
    {
        // Determine camera model directory name
        var cameraModel = string.IsNullOrEmpty(photo.CameraModel) ? "Unknown" : photo.CameraModel;

        // Build directory name: camera model + all unique source directories from which files came
        string directoryName;
        if (sourceDirectories != null && sourceDirectories.Any())
        {
            // Append all source directory names to camera model name with underscore separator
            var sourceDirectoriesSuffix = string.Join("_", sourceDirectories);
            directoryName = $"{cameraModel}_{sourceDirectoriesSuffix}";
        }
        else
        {
            // No source directories, use just camera model
            directoryName = cameraModel;
        }

        var cameraModelDirectory = Path.Combine(targetDirectory, directoryName);
        return Path.Combine(cameraModelDirectory, photo.FileName);
    }
}