using System.Text.Json;
using PhotoTransfer.Models;

namespace PhotoTransfer.Services;

public class MetadataStore
{
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        TypeInfoResolver = JsonContext.Default
    };

    public void SaveIndex(PhotoIndex index, string filePath)
    {
        try
        {
            var directoryPath = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directoryPath) && !Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            var jsonContent = JsonSerializer.Serialize(index, JsonContext.Default.PhotoIndex);
            File.WriteAllText(filePath, jsonContent);
        }
        catch (UnauthorizedAccessException)
        {
            throw new InvalidOperationException("Permission denied: Cannot write metadata file. Check directory permissions.");
        }
        catch (DirectoryNotFoundException)
        {
            throw new InvalidOperationException("Directory not found: Cannot create metadata file location.");
        }
    }

    public PhotoIndex LoadIndex(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Metadata file not found: {filePath}");
        }

        try
        {
            var jsonContent = File.ReadAllText(filePath);
            var index = JsonSerializer.Deserialize(jsonContent, JsonContext.Default.PhotoIndex);
            
            if (index == null)
            {
                throw new InvalidOperationException("Invalid metadata file format");
            }

            return index;
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Invalid metadata file format: {ex.Message}");
        }
        catch (UnauthorizedAccessException)
        {
            throw new InvalidOperationException("Permission denied: Cannot read metadata file.");
        }
    }

    public bool MetadataExists(string filePath)
    {
        return File.Exists(filePath);
    }

    public void UpdatePhotoTransferStatus(string metadataFilePath, string photoHash, string transferredTo)
    {
        var index = LoadIndex(metadataFilePath);
        
        var photo = index.Photos.FirstOrDefault(p => p.Hash == photoHash);
        if (photo != null)
        {
            photo.IsTransferred = true;
            photo.TransferredTo = transferredTo;
        }

        SaveIndex(index, metadataFilePath);
    }

    public string GetNextIncrementalFilePath(string baseFilePath)
    {
        var directory = Path.GetDirectoryName(baseFilePath) ?? Environment.CurrentDirectory;
        var fileName = Path.GetFileNameWithoutExtension(baseFilePath);
        var extension = Path.GetExtension(baseFilePath);
        
        var nextIndex = GetNextFileIndex(directory, fileName, extension);
        return Path.Combine(directory, $"{fileName}-{nextIndex:D4}{extension}");
    }

    public string GetLatestIndexFile(string baseFilePath)
    {
        var directory = Path.GetDirectoryName(baseFilePath) ?? Environment.CurrentDirectory;
        var fileName = Path.GetFileNameWithoutExtension(baseFilePath);
        var extension = Path.GetExtension(baseFilePath);
        
        var pattern = $"{fileName}-*.json";
        var files = Directory.GetFiles(directory, pattern)
            .Where(f => IsIncrementalFile(f, fileName, extension))
            .OrderByDescending(f => GetFileIndex(f, fileName, extension))
            .ToList();

        return files.FirstOrDefault() ?? baseFilePath;
    }

    private int GetNextFileIndex(string directory, string baseFileName, string extension)
    {
        var pattern = $"{baseFileName}-*.json";
        var existingFiles = Directory.GetFiles(directory, pattern);
        
        var maxIndex = 0;
        foreach (var file in existingFiles)
        {
            var index = GetFileIndex(file, baseFileName, extension);
            if (index > maxIndex)
            {
                maxIndex = index;
            }
        }
        
        return maxIndex + 1;
    }

    private int GetFileIndex(string filePath, string baseFileName, string extension)
    {
        var fileName = Path.GetFileNameWithoutExtension(filePath);
        var prefix = $"{baseFileName}-";
        
        if (fileName.StartsWith(prefix))
        {
            var indexPart = fileName.Substring(prefix.Length);
            if (int.TryParse(indexPart, out var index))
            {
                return index;
            }
        }
        
        return 0;
    }

    private bool IsIncrementalFile(string filePath, string baseFileName, string extension)
    {
        var fileName = Path.GetFileNameWithoutExtension(filePath);
        var fileExtension = Path.GetExtension(filePath);
        
        return fileExtension.Equals(extension, StringComparison.OrdinalIgnoreCase) &&
               fileName.StartsWith($"{baseFileName}-") &&
               int.TryParse(fileName.Substring($"{baseFileName}-".Length), out _);
    }

    public void SaveProgress(IndexingProgress progress, string filePath)
    {
        try
        {
            var directoryPath = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directoryPath) && !Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            var jsonContent = JsonSerializer.Serialize(progress, JsonContext.Default.IndexingProgress);
            File.WriteAllText(filePath, jsonContent);
        }
        catch (UnauthorizedAccessException)
        {
            throw new InvalidOperationException("Permission denied: Cannot write progress file.");
        }
    }

    public IndexingProgress? LoadProgress(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return null;
        }

        try
        {
            var jsonContent = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize(jsonContent, JsonContext.Default.IndexingProgress);
        }
        catch
        {
            return null;
        }
    }

    public void DeleteProgress(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
        catch
        {
            // Ignore errors when deleting progress file
        }
    }
}