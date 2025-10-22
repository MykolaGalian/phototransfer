using PhotoTransfer.Models;
using System.Security.Cryptography;

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

    // ========== OPTIMIZED ASYNC METHODS ==========

    /// <summary>
    /// Computes SHA256 hash of a file asynchronously
    /// </summary>
    private async Task<string> ComputeFileHashAsync(string filePath)
    {
        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 81920, useAsync: true);
        using var sha256 = SHA256.Create();
        var hashBytes = await sha256.ComputeHashAsync(stream);
        return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
    }

    /// <summary>
    /// Checks if target file exists and has the same hash as expected
    /// </summary>
    private async Task<bool> IsFileAlreadyTransferredAsync(string targetPath, string expectedHash)
    {
        if (!File.Exists(targetPath))
        {
            return false;
        }

        try
        {
            var targetHash = await ComputeFileHashAsync(targetPath);
            return targetHash.Equals(expectedHash, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Executes transfer operations in parallel with progress reporting and integrity verification
    /// </summary>
    public async Task ExecuteTransferAsync(
        List<TransferOperation> operations,
        bool dryRun = false,
        bool verifyIntegrity = true,
        bool skipExisting = true,
        int maxDegreeOfParallelism = 4,
        IProgress<TransferProgress>? progress = null)
    {
        if (operations.Count == 0)
        {
            return;
        }

        var totalOperations = operations.Count;
        var completedCount = 0;
        var skippedCount = 0;
        var failedCount = 0;
        var totalBytesTransferred = 0L;
        var startTime = DateTime.Now;

        var options = new ParallelOptions
        {
            MaxDegreeOfParallelism = maxDegreeOfParallelism
        };

        await Parallel.ForEachAsync(operations, options, async (operation, cancellationToken) =>
        {
            try
            {
                operation.Status = OperationStatus.InProgress;

                if (dryRun)
                {
                    operation.Status = OperationStatus.Completed;
                    Interlocked.Increment(ref completedCount);
                    ReportProgress(progress, totalOperations, completedCount, skippedCount, failedCount, totalBytesTransferred, startTime);
                    return;
                }

                // Check if file is already transferred with correct hash
                if (skipExisting && await IsFileAlreadyTransferredAsync(operation.TargetPath, operation.Photo.Hash))
                {
                    operation.Status = OperationStatus.Completed;
                    operation.ErrorMessage = "Skipped (already exists with same hash)";
                    Interlocked.Increment(ref skippedCount);
                    Interlocked.Increment(ref completedCount);
                    ReportProgress(progress, totalOperations, completedCount, skippedCount, failedCount, totalBytesTransferred, startTime);
                    return;
                }

                // Ensure target directory exists
                var targetDir = Path.GetDirectoryName(operation.TargetPath);
                if (!string.IsNullOrEmpty(targetDir))
                {
                    lock (typeof(PhotoTransferService)) // Prevent race condition on directory creation
                    {
                        if (!Directory.Exists(targetDir))
                        {
                            Directory.CreateDirectory(targetDir);
                        }
                    }
                }

                // Verify source file still exists
                if (!File.Exists(operation.Photo.FilePath))
                {
                    throw new FileNotFoundException($"Source file no longer exists: {operation.Photo.FilePath}");
                }

                // Perform the file operation
                if (operation.Type == TransferType.Copy)
                {
                    await CopyFileAsync(operation.Photo.FilePath, operation.TargetPath);
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

                // Verify integrity if requested
                if (verifyIntegrity && operation.Type == TransferType.Copy)
                {
                    var targetHash = await ComputeFileHashAsync(operation.TargetPath);
                    if (!targetHash.Equals(operation.Photo.Hash, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidOperationException("File integrity check failed: hash mismatch after copy");
                    }
                }

                operation.Status = OperationStatus.Completed;
                Interlocked.Increment(ref completedCount);
                Interlocked.Add(ref totalBytesTransferred, operation.Photo.FileSize);
                ReportProgress(progress, totalOperations, completedCount, skippedCount, failedCount, totalBytesTransferred, startTime);
            }
            catch (Exception ex)
            {
                operation.Status = OperationStatus.Failed;
                operation.ErrorMessage = ex.Message;
                Interlocked.Increment(ref failedCount);
                Interlocked.Increment(ref completedCount);
                ReportProgress(progress, totalOperations, completedCount, skippedCount, failedCount, totalBytesTransferred, startTime);
            }
        });
    }

    /// <summary>
    /// Async file copy with buffering
    /// </summary>
    private async Task CopyFileAsync(string sourcePath, string destinationPath)
    {
        const int bufferSize = 81920; // 80KB buffer
        using var sourceStream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize, useAsync: true);
        using var destinationStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize, useAsync: true);
        await sourceStream.CopyToAsync(destinationStream);
    }

    /// <summary>
    /// Reports progress to the progress handler
    /// </summary>
    private void ReportProgress(
        IProgress<TransferProgress>? progress,
        int total,
        int completed,
        int skipped,
        int failed,
        long totalBytes,
        DateTime startTime)
    {
        if (progress == null) return;

        var elapsed = DateTime.Now - startTime;
        var rate = elapsed.TotalSeconds > 0 ? totalBytes / elapsed.TotalSeconds : 0;

        progress.Report(new TransferProgress
        {
            TotalOperations = total,
            CompletedOperations = completed,
            SkippedOperations = skipped,
            FailedOperations = failed,
            TotalBytesTransferred = totalBytes,
            BytesPerSecond = rate,
            ElapsedTime = elapsed
        });
    }

    /// <summary>
    /// Batch update metadata after transfer (optimized - single write)
    /// </summary>
    public void UpdateMetadataAfterTransferBatch(string metadataFilePath, List<TransferOperation> completedOperations)
    {
        var hashToPath = completedOperations
            .Where(op => op.Status == OperationStatus.Completed && op.ErrorMessage != "Skipped (already exists with same hash)")
            .ToDictionary(op => op.Photo.Hash, op => op.TargetPath);

        if (hashToPath.Count > 0)
        {
            _metadataStore.UpdatePhotoTransferStatusBatch(metadataFilePath, hashToPath);
        }
    }
}