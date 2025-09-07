using System.Text.Json;
using NUnit.Framework;
using PhotoTransfer.Models;
using PhotoTransfer.Services;

namespace PhotoTransfer.Tests.IntegrationTests;

/// <summary>
/// Integration tests for photo indexing workflow
/// Tests the complete scan → extract metadata → save JSON workflow
/// Uses real test directories with sample images
/// These tests MUST FAIL initially (TDD requirement)
/// </summary>
[TestFixture]
[Category("Integration")]
public class PhotoIndexingTests
{
    private string _testDirectory = string.Empty;
    private string _tempDirectory = string.Empty;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"phototransfer-indexing-{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDirectory);
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, true);
        }
    }

    [SetUp]
    public void SetUp()
    {
        _testDirectory = Path.Combine(_tempDirectory, $"test-{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);
    }

    [Test]
    public void IndexPhotos_WithMixedImageTypes_ShouldIndexAllSupportedFormats()
    {
        // Arrange: Create directory with various image types
        CreateTestImage(_testDirectory, "photo1.jpg", CreateJpegHeader());
        CreateTestImage(_testDirectory, "photo2.png", CreatePngHeader());
        CreateTestImage(_testDirectory, "photo3.gif", CreateGifHeader());
        CreateTestImage(_testDirectory, "photo4.bmp", CreateBmpHeader());
        CreateTestImage(_testDirectory, "photo5.tiff", CreateTiffHeader());
        
        // Add non-image file that should be ignored
        File.WriteAllText(Path.Combine(_testDirectory, "readme.txt"), "Not an image");

        // Act: Index the directory (this will fail until PhotoIndexer is implemented)
        var indexer = new PhotoIndexer(); // This class doesn't exist yet - test MUST fail
        var outputPath = Path.Combine(_testDirectory, ".phototransfer-index.json");
        var result = indexer.IndexDirectory(_testDirectory, outputPath);

        // Assert: Should find all 5 image files, ignore text file
        Assert.That(result.Photos.Count, Is.EqualTo(5), "Should index exactly 5 image files");
        Assert.That(result.SupportedExtensions, Contains.Item(".jpg"));
        Assert.That(result.SupportedExtensions, Contains.Item(".png"));
        Assert.That(result.SupportedExtensions, Contains.Item(".gif"));
        Assert.That(result.SupportedExtensions, Contains.Item(".bmp"));
        Assert.That(result.SupportedExtensions, Contains.Item(".tiff"));

        // Verify each photo has required metadata
        foreach (var photo in result.Photos)
        {
            Assert.That(photo.FilePath, Is.Not.Empty, "FilePath should be populated");
            Assert.That(photo.FileName, Is.Not.Empty, "FileName should be populated");
            Assert.That(photo.Extension, Is.Not.Empty, "Extension should be populated");
            Assert.That(photo.FileSize, Is.GreaterThan(0), "FileSize should be greater than 0");
            Assert.That(photo.Hash, Is.Not.Empty, "Hash should be generated");
            Assert.That(photo.CreationDate, Is.Not.EqualTo(default(DateTime)), "CreationDate should be set");
        }
    }

    [Test]
    public void IndexPhotos_WithSubdirectories_ShouldIndexRecursively()
    {
        // Arrange: Create nested directory structure
        var subDir1 = Path.Combine(_testDirectory, "2023", "January");
        var subDir2 = Path.Combine(_testDirectory, "2023", "February");
        var subDir3 = Path.Combine(_testDirectory, "2024");
        
        Directory.CreateDirectory(subDir1);
        Directory.CreateDirectory(subDir2);
        Directory.CreateDirectory(subDir3);

        CreateTestImage(subDir1, "vacation1.jpg", CreateJpegHeader());
        CreateTestImage(subDir1, "vacation2.jpg", CreateJpegHeader());
        CreateTestImage(subDir2, "family.png", CreatePngHeader());
        CreateTestImage(subDir3, "recent.gif", CreateGifHeader());
        CreateTestImage(_testDirectory, "root.jpg", CreateJpegHeader()); // Root level

        // Act: Index with recursive scanning
        var indexer = new PhotoIndexer(); // This class doesn't exist yet
        var outputPath = Path.Combine(_testDirectory, ".phototransfer-index.json");
        var result = indexer.IndexDirectory(_testDirectory, outputPath);

        // Assert: Should find all photos across all subdirectories
        Assert.That(result.Photos.Count, Is.EqualTo(5), "Should find photos in all subdirectories");
        Assert.That(result.WorkingDirectory, Is.EqualTo(_testDirectory), "Should record working directory");
        
        // Verify photos from different subdirectories are included
        var filePaths = result.Photos.Select(p => p.FilePath).ToList();
        Assert.That(filePaths.Any(p => p.Contains("January")), Is.True, "Should include photos from January subdir");
        Assert.That(filePaths.Any(p => p.Contains("February")), Is.True, "Should include photos from February subdir");
        Assert.That(filePaths.Any(p => p.Contains("2024")), Is.True, "Should include photos from 2024 subdir");
        Assert.That(filePaths.Any(p => p.EndsWith("root.jpg")), Is.True, "Should include root level photos");
    }

    [Test]
    public void IndexPhotos_WithExifMetadata_ShouldExtractCreationDate()
    {
        // Arrange: Create image with EXIF date metadata
        var jpegWithExif = CreateJpegWithExifDate(new DateTime(2022, 8, 15, 14, 30, 0));
        CreateTestImage(_testDirectory, "with-exif.jpg", jpegWithExif);
        
        // Create image without EXIF - should fall back to file date
        CreateTestImage(_testDirectory, "without-exif.jpg", CreateJpegHeader());
        var fileInfo = new FileInfo(Path.Combine(_testDirectory, "without-exif.jpg"));
        var expectedFileDate = fileInfo.CreationTime;

        // Act: Index and extract metadata
        var indexer = new PhotoIndexer(); // This class doesn't exist yet
        var outputPath = Path.Combine(_testDirectory, ".phototransfer-index.json");
        var result = indexer.IndexDirectory(_testDirectory, outputPath);

        // Assert: Should extract different dates based on available metadata
        Assert.That(result.Photos.Count, Is.EqualTo(2));
        
        var photoWithExif = result.Photos.First(p => p.FileName == "with-exif.jpg");
        var photoWithoutExif = result.Photos.First(p => p.FileName == "without-exif.jpg");

        // Photo with EXIF should use EXIF date
        Assert.That(photoWithExif.CreationDate.Date, Is.EqualTo(new DateTime(2022, 8, 15).Date), 
            "Should extract creation date from EXIF metadata");

        // Photo without EXIF should use file creation date
        Assert.That(photoWithoutExif.CreationDate.Date, Is.EqualTo(expectedFileDate.Date),
            "Should fall back to file creation date when EXIF is unavailable");
    }

    [Test]
    public void SaveIndex_WithValidData_ShouldCreateJsonMetadataFile()
    {
        // Arrange: Create sample photo index data
        var photos = new List<PhotoMetadata>
        {
            new PhotoMetadata
            {
                FilePath = Path.Combine(_testDirectory, "photo1.jpg"),
                FileName = "photo1.jpg",
                CreationDate = new DateTime(2023, 6, 15),
                FileSize = 1024,
                Extension = ".jpg",
                Hash = "abc123def456",
                IsTransferred = false,
                TransferredTo = null
            }
        };

        var index = new PhotoIndex
        {
            IndexedAt = DateTime.UtcNow,
            WorkingDirectory = _testDirectory,
            Photos = photos,
            Version = "1.0.0",
            TotalCount = 1,
            SupportedExtensions = new[] { ".jpg", ".png", ".gif", ".bmp", ".tiff" }
        };

        // Act: Save to metadata file (MetadataStore doesn't exist yet)
        var metadataStore = new MetadataStore(); // This class doesn't exist yet
        var metadataPath = Path.Combine(_testDirectory, ".phototransfer-index.json");
        metadataStore.SaveIndex(index, metadataPath);

        // Assert: Should create valid JSON file
        Assert.That(File.Exists(metadataPath), Is.True, "Metadata file should be created");

        var jsonContent = File.ReadAllText(metadataPath);
        var deserializedIndex = JsonSerializer.Deserialize<PhotoIndex>(jsonContent);

        Assert.That(deserializedIndex, Is.Not.Null, "Should be valid JSON");
        Assert.That(deserializedIndex.Photos.Count, Is.EqualTo(1), "Should preserve photo count");
        Assert.That(deserializedIndex.WorkingDirectory, Is.EqualTo(_testDirectory), "Should preserve working directory");
        Assert.That(deserializedIndex.Version, Is.EqualTo("1.0.0"), "Should preserve version");

        // Verify photo data integrity
        var photo = deserializedIndex.Photos.First();
        Assert.That(photo.FileName, Is.EqualTo("photo1.jpg"), "Should preserve filename");
        Assert.That(photo.Extension, Is.EqualTo(".jpg"), "Should preserve extension");
        Assert.That(photo.Hash, Is.EqualTo("abc123def456"), "Should preserve hash");
    }

    [Test]
    public void LoadIndex_WithExistingMetadata_ShouldDeserializeCorrectly()
    {
        // Arrange: Create metadata file manually
        var metadata = new
        {
            indexedAt = DateTime.UtcNow,
            workingDirectory = _testDirectory,
            version = "1.0.0",
            totalCount = 2,
            supportedExtensions = new[] { ".jpg", ".png" },
            photos = new[]
            {
                new
                {
                    filePath = Path.Combine(_testDirectory, "test1.jpg"),
                    fileName = "test1.jpg",
                    creationDate = new DateTime(2023, 1, 1),
                    fileSize = 2048L,
                    extension = ".jpg",
                    hash = "hash1",
                    isTransferred = false,
                    transferredTo = (string?)null
                }
            }
        };

        var jsonContent = JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true });
        var metadataPath = Path.Combine(_testDirectory, ".phototransfer-index.json");
        File.WriteAllText(metadataPath, jsonContent);

        // Act: Load metadata (MetadataStore doesn't exist yet)
        var metadataStore = new MetadataStore(); // This class doesn't exist yet
        var loadedIndex = metadataStore.LoadIndex(metadataPath);

        // Assert: Should load data correctly
        Assert.That(loadedIndex, Is.Not.Null, "Should load index successfully");
        Assert.That(loadedIndex.Photos.Count, Is.EqualTo(1), "Should load correct number of photos");
        Assert.That(loadedIndex.WorkingDirectory, Is.EqualTo(_testDirectory), "Should load working directory");
        Assert.That(loadedIndex.Version, Is.EqualTo("1.0.0"), "Should load version");

        var photo = loadedIndex.Photos.First();
        Assert.That(photo.FileName, Is.EqualTo("test1.jpg"), "Should load photo filename");
        Assert.That(photo.CreationDate.Date, Is.EqualTo(new DateTime(2023, 1, 1)), "Should load creation date");
    }

    [Test]
    public void IndexPhotos_WithEmptyDirectory_ShouldCreateEmptyIndex()
    {
        // Arrange: Empty directory
        var emptyDir = Path.Combine(_testDirectory, "empty");
        Directory.CreateDirectory(emptyDir);

        // Act: Index empty directory
        var indexer = new PhotoIndexer(); // This class doesn't exist yet
        var outputPath = Path.Combine(emptyDir, ".phototransfer-index.json");
        var result = indexer.IndexDirectory(emptyDir, outputPath);

        // Assert: Should create valid but empty index
        Assert.That(result, Is.Not.Null, "Should return valid index even for empty directory");
        Assert.That(result.Photos.Count, Is.EqualTo(0), "Should have no photos");
        Assert.That(result.TotalCount, Is.EqualTo(0), "TotalCount should be 0");
        Assert.That(result.WorkingDirectory, Is.EqualTo(emptyDir), "Should record working directory");
        Assert.That(result.IndexedAt, Is.LessThanOrEqualTo(DateTime.UtcNow), "Should set indexed timestamp");
    }

    #region Helper Methods

    private void CreateTestImage(string directory, string fileName, byte[] content)
    {
        var filePath = Path.Combine(directory, fileName);
        File.WriteAllBytes(filePath, content);
    }

    private byte[] CreateJpegHeader()
    {
        // Minimal JPEG file header
        return new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46 };
    }

    private byte[] CreatePngHeader()
    {
        // PNG file signature
        return new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
    }

    private byte[] CreateGifHeader()
    {
        // GIF file header
        return new byte[] { 0x47, 0x49, 0x46, 0x38, 0x39, 0x61 }; // GIF89a
    }

    private byte[] CreateBmpHeader()
    {
        // BMP file header
        return new byte[] { 0x42, 0x4D, 0x00, 0x00, 0x00, 0x00 }; // BM
    }

    private byte[] CreateTiffHeader()
    {
        // TIFF file header (little endian)
        return new byte[] { 0x49, 0x49, 0x2A, 0x00 };
    }

    private byte[] CreateJpegWithExifDate(DateTime date)
    {
        // This is a simplified approach - in reality, EXIF is complex
        // For testing, we'll create a minimal JPEG with a fake EXIF section
        var header = CreateJpegHeader();
        var fakeExifData = System.Text.Encoding.ASCII.GetBytes($"EXIF{date:yyyy:MM:dd HH:mm:ss}");
        
        var combined = new byte[header.Length + fakeExifData.Length];
        Array.Copy(header, 0, combined, 0, header.Length);
        Array.Copy(fakeExifData, 0, combined, header.Length, fakeExifData.Length);
        
        return combined;
    }

    #endregion
}