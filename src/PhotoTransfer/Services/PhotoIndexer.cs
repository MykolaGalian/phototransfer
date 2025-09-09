using System.Security.Cryptography;
using System.Text.Json;
using PhotoTransfer.Models;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using MetadataExtractor.Formats.QuickTime;

namespace PhotoTransfer.Services;

public class PhotoIndexer
{
    private readonly string[] _supportedExtensions = { 
        ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".tiff", ".tif",
        ".cr3", ".crw", ".cr2", ".avi", ".mp4", ".3gp", ".m4a", ".mov",
        ".jpg_128x96", ".mp4_128x96"
    };

    public PhotoIndex IndexDirectory(string directoryPath, string outputFilePath, bool updateBase = false, Action<string>? progressCallback = null)
    {
        if (!System.IO.Directory.Exists(directoryPath))
        {
            throw new DirectoryNotFoundException($"Directory not found: {directoryPath}");
        }

        var metadataStore = new MetadataStore();
        var baseIndexPath = Path.Combine(Path.GetDirectoryName(outputFilePath) ?? directoryPath, "base-index.json");
        var progressFilePath = Path.ChangeExtension(outputFilePath, ".progress");
        
        // Phase 1: Create or load base index with all file paths
        var progress = metadataStore.LoadProgress(progressFilePath);
        if (progress == null || updateBase)
        {
            if (updateBase)
            {
                progressCallback?.Invoke("Updating base index with new formats...");
                progress = UpdateBaseIndexWithNewFormats(directoryPath, outputFilePath, baseIndexPath, metadataStore, progressCallback);
            }
            else
            {
                progressCallback?.Invoke("Creating base index...");
                progress = CreateBaseIndex(directoryPath, outputFilePath, baseIndexPath, progressCallback);
            }
            metadataStore.SaveProgress(progress, progressFilePath);
        }

        // Phase 2: Process files incrementally with 5000-record saves
        return ProcessFilesIncrementally(progress, progressFilePath, metadataStore, progressCallback);
    }

    private IndexingProgress CreateBaseIndex(string directoryPath, string outputFilePath, string baseIndexPath, Action<string>? progressCallback)
    {
        var progress = new IndexingProgress
        {
            WorkingDirectory = directoryPath,
            StartedAt = DateTime.UtcNow,
            LastSavedAt = DateTime.UtcNow,
            CurrentOutputFile = outputFilePath,
            AllFilePaths = new List<string>(),
            ProcessedFilePaths = new HashSet<string>()
        };

        // Collect all valid file paths
        var allFiles = System.IO.Directory.GetFiles(directoryPath, "*", SearchOption.AllDirectories);
        
        foreach (var filePath in allFiles)
        {
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            if (Array.Exists(_supportedExtensions, ext => ext == extension))
            {
                try
                {
                    var fileInfo = new FileInfo(filePath);
                    if (fileInfo.Length > 0)
                    {
                        progress.AllFilePaths.Add(filePath);
                    }
                }
                catch
                {
                    // Skip files that can't be accessed
                }
            }
        }
        
        progress.TotalFiles = progress.AllFilePaths.Count;
        
        // Save base index file
        var baseIndexContent = new BaseIndex
        {
            CreatedAt = DateTime.UtcNow,
            WorkingDirectory = directoryPath,
            TotalFiles = progress.TotalFiles,
            FilePaths = progress.AllFilePaths
        };
        
        var baseIndexJson = JsonSerializer.Serialize(baseIndexContent, JsonContext.Default.BaseIndex);
        File.WriteAllText(baseIndexPath, baseIndexJson);
        
        progressCallback?.Invoke($"Base index created: {progress.TotalFiles} files found");
        
        return progress;
    }

    private PhotoIndex ProcessFilesIncrementally(IndexingProgress progress, string progressFilePath, 
        MetadataStore metadataStore, Action<string>? progressCallback)
    {
        var photos = new List<PhotoMetadata>();
        const int saveInterval = 5000;

        // Load existing metadata if available
        var latestIndexFile = metadataStore.GetLatestIndexFile(Path.Combine(progress.WorkingDirectory, ".phototransfer-index.json"));
        if (File.Exists(latestIndexFile) && progress.ProcessedFiles > 0)
        {
            try
            {
                var existingIndex = metadataStore.LoadIndex(latestIndexFile);
                // Add existing photos that are still valid
                foreach (var existingPhoto in existingIndex.Photos)
                {
                    if (progress.AllFilePaths.Contains(existingPhoto.FilePath))
                    {
                        photos.Add(existingPhoto);
                    }
                }
                progressCallback?.Invoke($"Loaded {photos.Count} existing metadata records");
            }
            catch
            {
                progressCallback?.Invoke("Could not load existing metadata, processing all files");
            }
        }
        
        for (int i = 0; i < progress.AllFilePaths.Count; i++)
        {
            var filePath = progress.AllFilePaths[i];
            
            // Skip already processed files
            if (progress.ProcessedFilePaths.Contains(filePath))
            {
                continue;
            }

            try
            {
                var metadata = CreatePhotoMetadata(filePath);
                photos.Add(metadata);
                progress.ProcessedFilePaths.Add(filePath);
                progress.ProcessedFiles++;
                
                // Report progress
                progressCallback?.Invoke($"Processing: {Path.GetFileName(filePath)} ({progress.ProcessedFiles}/{progress.TotalFiles})");

                // Save progress every 5000 files
                if (progress.ProcessedFiles % saveInterval == 0)
                {
                    SaveIntermediateIndex(photos, progress, metadataStore);
                    progress.LastSavedAt = DateTime.UtcNow;
                    metadataStore.SaveProgress(progress, progressFilePath);
                    
                    progressCallback?.Invoke($"Saved progress: {progress.ProcessedFiles}/{progress.TotalFiles} files processed");
                }
            }
            catch (UnauthorizedAccessException)
            {
                progress.ProcessedFilePaths.Add(filePath);
                continue;
            }
            catch (IOException)
            {
                progress.ProcessedFilePaths.Add(filePath);
                continue;
            }
            catch
            {
                progress.ProcessedFilePaths.Add(filePath);
                continue;
            }
        }

        // Create final index and cleanup
        var finalIndex = new PhotoIndex
        {
            IndexedAt = DateTime.UtcNow,
            WorkingDirectory = progress.WorkingDirectory,
            Version = "1.0.0",
            TotalCount = photos.Count,
            SupportedExtensions = _supportedExtensions,
            Photos = photos
        };

        // Clean up progress file (keep base-index for reference)
        metadataStore.DeleteProgress(progressFilePath);
        
        return finalIndex;
    }

    private IndexingProgress UpdateBaseIndexWithNewFormats(string directoryPath, string outputFilePath, string baseIndexPath, 
        MetadataStore metadataStore, Action<string>? progressCallback)
    {
        // Load existing metadata from latest index file
        var latestIndexFile = metadataStore.GetLatestIndexFile(Path.Combine(directoryPath, ".phototransfer-index.json"));
        PhotoIndex? existingIndex = null;
        
        if (File.Exists(latestIndexFile))
        {
            try
            {
                existingIndex = metadataStore.LoadIndex(latestIndexFile);
                progressCallback?.Invoke($"Loaded existing index: {existingIndex.Photos.Count} photos");
            }
            catch
            {
                progressCallback?.Invoke("Could not load existing index, starting fresh");
            }
        }

        var progress = new IndexingProgress
        {
            WorkingDirectory = directoryPath,
            StartedAt = DateTime.UtcNow,
            LastSavedAt = DateTime.UtcNow,
            CurrentOutputFile = outputFilePath,
            AllFilePaths = new List<string>(),
            ProcessedFilePaths = new HashSet<string>()
        };

        // Collect all valid file paths with current supported extensions
        var allFiles = System.IO.Directory.GetFiles(directoryPath, "*", SearchOption.AllDirectories);
        
        foreach (var filePath in allFiles)
        {
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            if (Array.Exists(_supportedExtensions, ext => ext == extension))
            {
                try
                {
                    var fileInfo = new FileInfo(filePath);
                    if (fileInfo.Length > 0)
                    {
                        progress.AllFilePaths.Add(filePath);
                    }
                }
                catch
                {
                    // Skip files that can't be accessed
                }
            }
        }
        
        progress.TotalFiles = progress.AllFilePaths.Count;

        // If we have existing metadata, mark those files as already processed
        if (existingIndex != null)
        {
            foreach (var existingPhoto in existingIndex.Photos)
            {
                if (progress.AllFilePaths.Contains(existingPhoto.FilePath))
                {
                    progress.ProcessedFilePaths.Add(existingPhoto.FilePath);
                }
            }
            progress.ProcessedFiles = progress.ProcessedFilePaths.Count;
            progressCallback?.Invoke($"Found {progress.ProcessedFiles} already processed files");
        }

        // Save updated base index file
        var baseIndexContent = new BaseIndex
        {
            CreatedAt = DateTime.UtcNow,
            WorkingDirectory = directoryPath,
            TotalFiles = progress.TotalFiles,
            FilePaths = progress.AllFilePaths
        };
        
        var baseIndexJson = JsonSerializer.Serialize(baseIndexContent, JsonContext.Default.BaseIndex);
        File.WriteAllText(baseIndexPath, baseIndexJson);
        
        var newFiles = progress.TotalFiles - progress.ProcessedFiles;
        progressCallback?.Invoke($"Updated base index: {progress.TotalFiles} total files ({newFiles} new files to process)");
        
        return progress;
    }

    private void SaveIntermediateIndex(List<PhotoMetadata> photos, IndexingProgress progress, MetadataStore metadataStore)
    {
        var intermediateIndex = new PhotoIndex
        {
            IndexedAt = DateTime.UtcNow,
            WorkingDirectory = progress.WorkingDirectory,
            Version = "1.0.0-intermediate",
            TotalCount = photos.Count,
            SupportedExtensions = _supportedExtensions,
            Photos = photos
        };

        metadataStore.SaveIndex(intermediateIndex, progress.CurrentOutputFile);
    }

    private PhotoMetadata CreatePhotoMetadata(string filePath)
    {
        var fileInfo = new FileInfo(filePath);
        var hash = CalculateFileHash(filePath);
        var (creationDate, modificationDate, effectiveDate, allDates) = ExtractDates(filePath, fileInfo);

        return new PhotoMetadata
        {
            FilePath = filePath,
            FileName = fileInfo.Name,
            Extension = fileInfo.Extension.ToLowerInvariant(),
            FileSize = fileInfo.Length,
            Hash = hash,
            CreationDate = creationDate,
            ModificationDate = modificationDate,
            EffectiveDate = effectiveDate,
            AllDates = allDates,
            IsTransferred = false,
            TransferredTo = null
        };
    }

    private string CalculateFileHash(string filePath)
    {
        using var sha256 = SHA256.Create();
        using var stream = File.OpenRead(filePath);
        var hashBytes = sha256.ComputeHash(stream);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    private (DateTime creationDate, DateTime modificationDate, DateTime effectiveDate, List<DateSource> allDates) ExtractDates(string filePath, FileInfo fileInfo)
    {
        // Collect all possible date sources with metadata
        var allDates = new List<DateSource>();
        
        // Add filesystem dates
        allDates.Add(new DateSource 
        { 
            Date = fileInfo.CreationTime, 
            Source = "FileSystem.Creation",
            IsPlaceholder = IsPlaceholderDate(fileInfo.CreationTime)
        });
        allDates.Add(new DateSource 
        { 
            Date = fileInfo.LastWriteTime, 
            Source = "FileSystem.LastWrite",
            IsPlaceholder = IsPlaceholderDate(fileInfo.LastWriteTime)
        });
        
        // Try to extract metadata dates (EXIF for photos, video metadata for videos)
        var metadataDates = TryExtractMetadataDates(filePath);
        allDates.AddRange(metadataDates);
        
        var creationDate = fileInfo.CreationTime;
        var modificationDate = fileInfo.LastWriteTime;
        
        // Smart date selection with EXIF priority
        var effectiveDate = SelectBestDate(allDates, creationDate);

        return (creationDate, modificationDate, effectiveDate, allDates);
    }

    private DateTime SelectBestDate(List<DateSource> allDates, DateTime fallbackDate)
    {
        // Filter valid dates (not placeholder, not invalid)
        var validDates = allDates
            .Where(ds => ds.Date > DateTime.MinValue && ds.Date < DateTime.MaxValue && !ds.IsPlaceholder)
            .ToList();

        if (!validDates.Any())
        {
            // No valid dates found, use any valid date or fallback
            var anyValidDates = allDates
                .Where(ds => ds.Date > DateTime.MinValue && ds.Date < DateTime.MaxValue)
                .ToList();
            
            return anyValidDates.Any() ? anyValidDates.Min(ds => ds.Date) : fallbackDate;
        }

        // Separate EXIF and filesystem dates
        var exifDates = validDates.Where(ds => ds.Source.StartsWith("EXIF.") || ds.Source.StartsWith("Legacy.EXIF")).ToList();
        var filesystemDates = validDates.Where(ds => ds.Source.StartsWith("FileSystem.")).ToList();
        var otherDates = validDates.Where(ds => !ds.Source.StartsWith("EXIF.") && !ds.Source.StartsWith("Legacy.EXIF") && !ds.Source.StartsWith("FileSystem.")).ToList();

        // If we have EXIF dates, check if filesystem dates are suspicious
        if (exifDates.Any() && filesystemDates.Any())
        {
            var bestExifDate = exifDates.Min(ds => ds.Date);
            var oldestFilesystemDate = filesystemDates.Min(ds => ds.Date);
            
            // If filesystem date is more than 1 year older than EXIF date, prefer EXIF
            if (bestExifDate.Subtract(oldestFilesystemDate).TotalDays > 365)
            {
                // EXIF date is much newer than filesystem - filesystem date is likely corrupted
                // Combine EXIF and other dates, ignore filesystem dates
                var reliableDates = exifDates.Concat(otherDates).ToList();
                return reliableDates.Min(ds => ds.Date);
            }
        }

        // Default behavior: use oldest valid date
        return validDates.Min(ds => ds.Date);
    }

    private bool IsPlaceholderDate(DateTime date)
    {
        // Common placeholder dates to ignore when selecting effective date
        var placeholderDates = new[]
        {
            new DateTime(2001, 1, 1), // Common default date
            new DateTime(1980, 1, 1), // Another common default
            new DateTime(1970, 1, 1), // Unix epoch
            new DateTime(1900, 1, 1), // Very old default
            new DateTime(2000, 1, 1), // Y2K default
        };
        
        // Check if date matches any placeholder (ignoring time component)
        return placeholderDates.Any(placeholder => 
            date.Year == placeholder.Year && 
            date.Month == placeholder.Month && 
            date.Day == placeholder.Day);
    }

    private List<DateSource> TryExtractMetadataDates(string filePath)
    {
        var dates = new List<DateSource>();
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        
        try
        {
            // For image files, extract EXIF dates
            if (IsImageFile(extension))
            {
                dates.AddRange(ExtractExifDates(filePath));
            }
            // For video files, extract video metadata dates
            else if (IsVideoFile(extension))
            {
                dates.AddRange(ExtractVideoDates(filePath));
            }
        }
        catch
        {
            // Ignore metadata extraction errors - will fall back to filesystem dates
        }
        
        return dates;
    }

    private bool IsImageFile(string extension)
    {
        return extension is ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" or ".tiff" or ".tif" or ".cr3" or ".crw" or ".cr2";
    }

    private bool IsVideoFile(string extension)
    {
        return extension is ".avi" or ".mp4" or ".3gp" or ".mov";
    }

    private List<DateSource> ExtractExifDates(string filePath)
    {
        var dates = new List<DateSource>();
        
        try
        {
            var directories = MetadataExtractor.ImageMetadataReader.ReadMetadata(filePath);
            
            // Try to get the most reliable EXIF dates in order of preference:
            // 1. DateTimeOriginal (when photo was taken)
            // 2. DateTime (general date/time)  
            // 3. DateTimeDigitized (when photo was digitized)
            
            foreach (var directory in directories)
            {
                if (directory.TryGetDateTime(MetadataExtractor.Formats.Exif.ExifDirectoryBase.TagDateTimeOriginal, out var dateTimeOriginal))
                    dates.Add(new DateSource 
                    { 
                        Date = dateTimeOriginal, 
                        Source = "EXIF.DateTimeOriginal",
                        IsPlaceholder = IsPlaceholderDate(dateTimeOriginal)
                    });
                    
                if (directory.TryGetDateTime(MetadataExtractor.Formats.Exif.ExifDirectoryBase.TagDateTime, out var dateTime))
                    dates.Add(new DateSource 
                    { 
                        Date = dateTime, 
                        Source = "EXIF.DateTime",
                        IsPlaceholder = IsPlaceholderDate(dateTime)
                    });
                    
                if (directory.TryGetDateTime(MetadataExtractor.Formats.Exif.ExifDirectoryBase.TagDateTimeDigitized, out var dateTimeDigitized))
                    dates.Add(new DateSource 
                    { 
                        Date = dateTimeDigitized, 
                        Source = "EXIF.DateTimeDigitized",
                        IsPlaceholder = IsPlaceholderDate(dateTimeDigitized)
                    });
            }
        }
        catch
        {
            // Fall back to old simple extraction for compatibility
            var legacyDate = TryExtractLegacyExifDate(filePath);
            if (legacyDate.HasValue)
                dates.Add(new DateSource 
                { 
                    Date = legacyDate.Value, 
                    Source = "Legacy.EXIF",
                    IsPlaceholder = IsPlaceholderDate(legacyDate.Value)
                });
        }
        
        return dates;
    }

    private List<DateSource> ExtractVideoDates(string filePath)
    {
        var dates = new List<DateSource>();
        
        try
        {
            var directories = MetadataExtractor.ImageMetadataReader.ReadMetadata(filePath);
            
            // For video files, try to extract creation time from various metadata directories
            foreach (var directory in directories)
            {
                // Try different approaches to get video dates
                if (directory.HasTagName(0x0000)) // Generic approach for creation time
                {
                    try
                    {
                        if (directory.TryGetDateTime(0x0000, out var creationTime))
                            dates.Add(new DateSource 
                            { 
                                Date = creationTime, 
                                Source = "Video.CreationTime",
                                IsPlaceholder = IsPlaceholderDate(creationTime)
                            });
                    }
                    catch { }
                }
                
                // Try to get modification time
                if (directory.HasTagName(0x0001))
                {
                    try
                    {
                        if (directory.TryGetDateTime(0x0001, out var modificationTime))
                            dates.Add(new DateSource 
                            { 
                                Date = modificationTime, 
                                Source = "Video.ModificationTime",
                                IsPlaceholder = IsPlaceholderDate(modificationTime)
                            });
                    }
                    catch { }
                }
            }
        }
        catch
        {
            // Video metadata extraction failed - will use filesystem dates only
        }
        
        return dates;
    }

    private DateTime? TryExtractLegacyExifDate(string filePath)
    {
        try
        {
            // Keep old simple extraction for test compatibility
            var bytes = File.ReadAllBytes(filePath);
            var content = System.Text.Encoding.ASCII.GetString(bytes);
            
            var exifPattern = @"EXIF(\d{4}):(\d{2}):(\d{2}) (\d{2}):(\d{2}):(\d{2})";
            var match = System.Text.RegularExpressions.Regex.Match(content, exifPattern);
            
            if (match.Success)
            {
                var year = int.Parse(match.Groups[1].Value);
                var month = int.Parse(match.Groups[2].Value);
                var day = int.Parse(match.Groups[3].Value);
                var hour = int.Parse(match.Groups[4].Value);
                var minute = int.Parse(match.Groups[5].Value);
                var second = int.Parse(match.Groups[6].Value);
                
                return new DateTime(year, month, day, hour, minute, second);
            }
        }
        catch
        {
            // Ignore extraction errors
        }
        
        return null;
    }
}