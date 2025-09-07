using System.Text.Json;
using NUnit.Framework;
using PhotoTransfer.Models;
using PhotoTransfer.Services;

namespace PhotoTransfer.Tests.IntegrationTests;

/// <summary>
/// Integration tests for photo transfer workflow
/// Tests the complete load index → filter by date → move files workflow
/// Verifies files are moved correctly with duplicate handling
/// These tests MUST FAIL initially (TDD requirement)
/// </summary>
[TestFixture]
[Category("Integration")]
public class PhotoTransferTests
{
    private string _testDirectory = string.Empty;
    private string _tempDirectory = string.Empty;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"phototransfer-transfer-{Guid.NewGuid()}");
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
    public void TransferPhotos_WithValidTimePeriod_ShouldMoveMatchingFiles()
    {
        // Arrange: Setup directory with photos and metadata
        var sourceDir = CreateSourceDirectoryWithPhotos();
        var metadataPath = CreateMetadataFile(sourceDir);

        var timePeriod = new TimePeriod(2023, 6);
        var targetBaseDir = Path.Combine(_testDirectory, "output");

        // Act: Transfer photos from June 2023 (PhotoTransferService doesn't exist yet)
        var transferService = new PhotoTransferService(); // This class doesn't exist yet
        var metadataStore = new MetadataStore();
        var index = metadataStore.LoadIndex(metadataPath);
        var photos = transferService.GetPhotosForPeriod(index, timePeriod);
        var targetDir = Path.Combine(targetBaseDir, timePeriod.ToString());
        var operations = transferService.PlanTransfer(photos, targetDir);
        transferService.ExecuteTransfer(operations);

        // Assert: Should move matching photos to target directory
        var expectedTargetDir = Path.Combine(targetBaseDir, "2023-06");
        Assert.That(Directory.Exists(expectedTargetDir), Is.True, "Target directory should be created");

        var transferredFiles = Directory.GetFiles(expectedTargetDir);
        Assert.That(transferredFiles.Length, Is.EqualTo(2), "Should transfer exactly 2 photos from June 2023");

        // Verify files are moved (not copied)
        Assert.That(File.Exists(Path.Combine(sourceDir, "june1.jpg")), Is.False, 
            "Original files should be moved (deleted from source)");
        Assert.That(File.Exists(Path.Combine(sourceDir, "june2.png")), Is.False,
            "Original files should be moved (deleted from source)");

        // Verify files exist in target
        Assert.That(File.Exists(Path.Combine(expectedTargetDir, "june1.jpg")), Is.True,
            "Files should exist in target directory");
        Assert.That(File.Exists(Path.Combine(expectedTargetDir, "june2.png")), Is.True,
            "Files should exist in target directory");

        // Other photos should remain untouched
        Assert.That(File.Exists(Path.Combine(sourceDir, "may1.jpg")), Is.True,
            "Photos from other periods should not be moved");
    }

    [Test]
    public void TransferPhotos_WithDuplicateNames_ShouldChooseLargestFile()
    {
        // Arrange: Create scenario with duplicate filenames
        var sourceDir = CreateDirectoryWithDuplicates();
        var metadataPath = CreateMetadataFileWithDuplicates(sourceDir);

        var timePeriod = new TimePeriod(2023, 8);
        var targetBaseDir = Path.Combine(_testDirectory, "output");

        // Act: Transfer photos with duplicate names
        var transferService = new PhotoTransferService();
        var metadataStore = new MetadataStore();
        var index = metadataStore.LoadIndex(metadataPath);
        var photos = transferService.GetPhotosForPeriod(index, timePeriod);
        var targetDir = Path.Combine(targetBaseDir, timePeriod.ToString());
        var operations = transferService.PlanTransfer(photos, targetDir);
        transferService.ExecuteTransfer(operations);

        // Assert: Should choose only the largest file, not create suffixed duplicates
        var expectedTargetDir = Path.Combine(targetBaseDir, "2023-08");
        
        // Check if operations were created and executed
        Assert.That(operations.Count, Is.EqualTo(1), "Should plan transfer for only the largest file");
        
        // Verify directory exists (should be created during ExecuteTransfer)
        Assert.That(Directory.Exists(expectedTargetDir), Is.True, "Target directory should be created");
        
        var files = Directory.GetFiles(expectedTargetDir).Select(Path.GetFileName).ToList();

        Assert.That(files.Count, Is.EqualTo(1), "Should transfer only the largest file");
        Assert.That(files.Contains("vacation.jpg"), Is.True, "Should have original filename without suffix");

        // Verify that the largest file (3000 bytes) was chosen
        var transferredFile = Path.Combine(expectedTargetDir, "vacation.jpg");
        var fileSize = new FileInfo(transferredFile).Length;
        Assert.That(fileSize, Is.EqualTo(3000), "Should have chosen the largest file (3000 bytes)");
    }

    [Test]
    public void TransferPhotos_WithCopyMode_ShouldPreserveOriginals()
    {
        // Arrange
        var sourceDir = CreateSourceDirectoryWithPhotos();
        var metadataPath = CreateMetadataFile(sourceDir);

        var timePeriod = new TimePeriod(2023, 6);
        var targetBaseDir = Path.Combine(_testDirectory, "output");

        // Act: Transfer in copy mode
        var transferService = new PhotoTransferService();
        var metadataStore = new MetadataStore();
        var index = metadataStore.LoadIndex(metadataPath);
        var photos = transferService.GetPhotosForPeriod(index, timePeriod);
        var targetDir = Path.Combine(targetBaseDir, timePeriod.ToString());
        var operations = transferService.PlanTransfer(photos, targetDir, TransferType.Copy);
        transferService.ExecuteTransfer(operations);

        // Assert: Should copy files, preserving originals
        var expectedTargetDir2 = Path.Combine(targetBaseDir, "2023-06");
        Assert.That(Directory.Exists(expectedTargetDir2), Is.True, "Target directory should be created");

        // Original files should still exist
        Assert.That(File.Exists(Path.Combine(sourceDir, "june1.jpg")), Is.True,
            "Original files should be preserved in copy mode");
        Assert.That(File.Exists(Path.Combine(sourceDir, "june2.png")), Is.True,
            "Original files should be preserved in copy mode");

        // Copied files should also exist
        Assert.That(File.Exists(Path.Combine(targetDir, "june1.jpg")), Is.True,
            "Files should be copied to target directory");
        Assert.That(File.Exists(Path.Combine(targetDir, "june2.png")), Is.True,
            "Files should be copied to target directory");
    }

    [Test]
    public void TransferPhotos_WithNoMatchingPhotos_ShouldReturnEmptyResult()
    {
        // Arrange: Photos from different time periods
        var sourceDir = CreateSourceDirectoryWithPhotos();
        var metadataPath = CreateMetadataFile(sourceDir);

        // Request photos from a period that doesn't exist
        var timePeriod = new TimePeriod(1995, 1);
        var targetBaseDir = Path.Combine(_testDirectory, "output");

        // Act: Attempt transfer
        var transferService = new PhotoTransferService();
        var metadataStore = new MetadataStore();
        var index = metadataStore.LoadIndex(metadataPath);
        var photos = transferService.GetPhotosForPeriod(index, timePeriod);
        var targetDir = Path.Combine(targetBaseDir, timePeriod.ToString());
        var operations = transferService.PlanTransfer(photos, targetDir);
        transferService.ExecuteTransfer(operations);

        // Assert: Should return empty result, no directories created
        Assert.That(photos.Count, Is.EqualTo(0), "Should find no photos to transfer");
        
        var expectedTargetDir3 = Path.Combine(targetBaseDir, "1995-01");
        Assert.That(Directory.Exists(expectedTargetDir3), Is.False, "Should not create target directory for empty result");

        // All original files should remain untouched
        Assert.That(File.Exists(Path.Combine(sourceDir, "june1.jpg")), Is.True);
        Assert.That(File.Exists(Path.Combine(sourceDir, "june2.png")), Is.True);
        Assert.That(File.Exists(Path.Combine(sourceDir, "may1.jpg")), Is.True);
    }

    [Test]
    public void TransferPhotos_WithInvalidMetadataFile_ShouldThrowException()
    {
        // Arrange: Invalid metadata file
        var invalidMetadataPath = Path.Combine(_testDirectory, "invalid.json");
        File.WriteAllText(invalidMetadataPath, "{ invalid json }");

        var timePeriod = new TimePeriod(2023, 6);
        var targetBaseDir = Path.Combine(_testDirectory, "output");

        // Act & Assert: Should throw exception for invalid metadata
        var transferService = new PhotoTransferService();
        var metadataStore = new MetadataStore();
        
        Assert.Throws<JsonException>(() => 
            metadataStore.LoadIndex(invalidMetadataPath),
            "Should throw exception for invalid JSON metadata");
    }

    [Test]
    public void TransferPhotos_WithMissingSourceFiles_ShouldHandleGracefully()
    {
        // Arrange: Metadata references files that don't exist
        var sourceDir = Path.Combine(_testDirectory, "missing-files");
        Directory.CreateDirectory(sourceDir);

        var metadata = CreateBasicMetadata(sourceDir);
        // Reference files that don't actually exist
        metadata.photos[0].filePath = Path.Combine(sourceDir, "missing1.jpg");
        metadata.photos[1].filePath = Path.Combine(sourceDir, "missing2.png");

        var metadataPath = Path.Combine(_testDirectory, ".phototransfer-index.json");
        File.WriteAllText(metadataPath, JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true }));

        var timePeriod = new TimePeriod(2023, 6);
        var targetBaseDir = Path.Combine(_testDirectory, "output");

        // Act: Attempt transfer with missing source files
        var transferService = new PhotoTransferService();
        var metadataStore = new MetadataStore();
        var index = metadataStore.LoadIndex(metadataPath);
        var photos = transferService.GetPhotosForPeriod(index, timePeriod);
        var targetDir = Path.Combine(targetBaseDir, timePeriod.ToString());
        var operations = transferService.PlanTransfer(photos, targetDir);
        transferService.ExecuteTransfer(operations);

        // Assert: Should handle missing files gracefully (possibly skip or error)
        var failedOperations = operations.Where(op => op.Status == OperationStatus.Failed).ToList();
        Assert.That(failedOperations.Count, Is.GreaterThan(0), 
            "Should report failed operations for missing files");
        
        foreach (var failure in failedOperations)
        {
            Assert.That(failure.ErrorMessage, Does.Contain("not found").Or.Contain("missing"),
                "Error message should indicate missing files");
        }
    }

    [Test]
    public void TransferPhotos_ShouldUpdateMetadataWithTransferStatus()
    {
        // Arrange
        var sourceDir = CreateSourceDirectoryWithPhotos();
        var metadataPath = CreateMetadataFile(sourceDir);

        var timePeriod = new TimePeriod(2023, 6);
        var targetBaseDir = Path.Combine(_testDirectory, "output");

        // Act: Transfer photos
        var transferService = new PhotoTransferService();
        var metadataStore = new MetadataStore();
        var index = metadataStore.LoadIndex(metadataPath);
        var photos = transferService.GetPhotosForPeriod(index, timePeriod);
        var targetDir = Path.Combine(targetBaseDir, timePeriod.ToString());
        var operations = transferService.PlanTransfer(photos, targetDir);
        transferService.ExecuteTransfer(operations);
        transferService.UpdateMetadataAfterTransfer(metadataPath, operations);

        // Assert: Metadata file should be updated with transfer status
        var updatedMetadata = JsonSerializer.Deserialize<dynamic>(File.ReadAllText(metadataPath));
        
        // Find transferred photos in metadata
        var metadataStore2 = new MetadataStore(); // This class doesn't exist yet
        var updatedIndex = metadataStore2.LoadIndex(metadataPath);

        var transferredPhotos = updatedIndex.Photos.Where(p => p.IsTransferred).ToList();
        Assert.That(transferredPhotos.Count, Is.EqualTo(2), "Should mark transferred photos in metadata");

        foreach (var photo in transferredPhotos)
        {
            Assert.That(photo.TransferredTo, Is.Not.Null.And.Not.Empty, 
                "Should record destination path for transferred photos");
            Assert.That(photo.TransferredTo, Does.Contain("2023-06"),
                "Destination path should include time period");
        }
    }

    #region Helper Methods

    private string CreateSourceDirectoryWithPhotos()
    {
        var sourceDir = Path.Combine(_testDirectory, "source");
        Directory.CreateDirectory(sourceDir);

        // Create photos with different dates
        File.WriteAllBytes(Path.Combine(sourceDir, "june1.jpg"), CreateFakeJpeg(1024));
        File.WriteAllBytes(Path.Combine(sourceDir, "june2.png"), CreateFakePng(2048));
        File.WriteAllBytes(Path.Combine(sourceDir, "may1.jpg"), CreateFakeJpeg(1500));

        return sourceDir;
    }

    private string CreateDirectoryWithDuplicates()
    {
        var sourceDir = Path.Combine(_testDirectory, "duplicates");
        Directory.CreateDirectory(sourceDir);

        var subDir1 = Path.Combine(sourceDir, "folder1");
        var subDir2 = Path.Combine(sourceDir, "folder2");
        var subDir3 = Path.Combine(sourceDir, "folder3");

        Directory.CreateDirectory(subDir1);
        Directory.CreateDirectory(subDir2);
        Directory.CreateDirectory(subDir3);

        // Create files with same name but different content
        File.WriteAllBytes(Path.Combine(subDir1, "vacation.jpg"), CreateFakeJpeg(1000));
        File.WriteAllBytes(Path.Combine(subDir2, "vacation.jpg"), CreateFakeJpeg(2000));
        File.WriteAllBytes(Path.Combine(subDir3, "vacation.jpg"), CreateFakeJpeg(3000));

        return sourceDir;
    }

    private string CreateMetadataFile(string sourceDir)
    {
        var metadata = CreateBasicMetadata(sourceDir);
        var metadataPath = Path.Combine(_testDirectory, ".phototransfer-index.json");
        
        File.WriteAllText(metadataPath, JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true }));
        return metadataPath;
    }

    private string CreateMetadataFileWithDuplicates(string sourceDir)
    {
        var metadata = new
        {
            IndexedAt = DateTime.UtcNow,
            WorkingDirectory = sourceDir,
            Version = "1.0.0",
            TotalCount = 3,
            SupportedExtensions = new[] { ".jpg" },
            Photos = new[]
            {
                new
                {
                    FilePath = Path.Combine(sourceDir, "folder1", "vacation.jpg"),
                    FileName = "vacation.jpg",
                    CreationDate = new DateTime(2023, 8, 1),
                    ModificationDate = new DateTime(2023, 8, 1),
                    EffectiveDate = new DateTime(2023, 8, 1),
                    FileSize = 1000L,
                    Extension = ".jpg",
                    Hash = "hash1",
                    IsTransferred = false,
                    TransferredTo = (string?)null
                },
                new
                {
                    FilePath = Path.Combine(sourceDir, "folder2", "vacation.jpg"),
                    FileName = "vacation.jpg",
                    CreationDate = new DateTime(2023, 8, 15),
                    ModificationDate = new DateTime(2023, 8, 15),
                    EffectiveDate = new DateTime(2023, 8, 15),
                    FileSize = 2000L,
                    Extension = ".jpg",
                    Hash = "hash2",
                    IsTransferred = false,
                    TransferredTo = (string?)null
                },
                new
                {
                    FilePath = Path.Combine(sourceDir, "folder3", "vacation.jpg"),
                    FileName = "vacation.jpg",
                    CreationDate = new DateTime(2023, 8, 30),
                    ModificationDate = new DateTime(2023, 8, 30),
                    EffectiveDate = new DateTime(2023, 8, 30),
                    FileSize = 3000L,
                    Extension = ".jpg",
                    Hash = "hash3",
                    IsTransferred = false,
                    TransferredTo = (string?)null
                }
            }
        };

        var metadataPath = Path.Combine(_testDirectory, ".phototransfer-index.json");
        File.WriteAllText(metadataPath, JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true }));
        return metadataPath;
    }

    private dynamic CreateBasicMetadata(string sourceDir)
    {
        return new
        {
            indexedAt = DateTime.UtcNow,
            workingDirectory = sourceDir,
            version = "1.0.0",
            totalCount = 3,
            supportedExtensions = new[] { ".jpg", ".png" },
            photos = new[]
            {
                new
                {
                    filePath = Path.Combine(sourceDir, "june1.jpg"),
                    fileName = "june1.jpg",
                    creationDate = new DateTime(2023, 6, 1),
                    fileSize = 1024L,
                    extension = ".jpg",
                    hash = "hash1",
                    isTransferred = false,
                    transferredTo = (string?)null
                },
                new
                {
                    filePath = Path.Combine(sourceDir, "june2.png"),
                    fileName = "june2.png",
                    creationDate = new DateTime(2023, 6, 15),
                    fileSize = 2048L,
                    extension = ".png",
                    hash = "hash2",
                    isTransferred = false,
                    transferredTo = (string?)null
                },
                new
                {
                    filePath = Path.Combine(sourceDir, "may1.jpg"),
                    fileName = "may1.jpg",
                    creationDate = new DateTime(2023, 5, 10),
                    fileSize = 1500L,
                    extension = ".jpg",
                    hash = "hash3",
                    isTransferred = false,
                    transferredTo = (string?)null
                }
            }
        };
    }

    private byte[] CreateFakeJpeg(int size)
    {
        var data = new byte[size];
        data[0] = 0xFF; data[1] = 0xD8; // JPEG header
        // Fill rest with random data
        new Random().NextBytes(data.Skip(2).ToArray());
        return data;
    }

    private byte[] CreateFakePng(int size)
    {
        var data = new byte[size];
        // PNG signature
        data[0] = 0x89; data[1] = 0x50; data[2] = 0x4E; data[3] = 0x47;
        data[4] = 0x0D; data[5] = 0x0A; data[6] = 0x1A; data[7] = 0x0A;
        // Fill rest with random data
        new Random().NextBytes(data.Skip(8).ToArray());
        return data;
    }

    #endregion
}