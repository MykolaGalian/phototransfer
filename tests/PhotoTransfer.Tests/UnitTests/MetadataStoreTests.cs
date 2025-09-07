using NUnit.Framework;
using PhotoTransfer.Models;
using PhotoTransfer.Services;
using System.Text.Json;

namespace PhotoTransfer.Tests.UnitTests;

[TestFixture]
[Category("Unit")]
public class MetadataStoreTests
{
    private string _testDirectory = string.Empty;
    private MetadataStore _metadataStore = null!;

    [SetUp]
    public void SetUp()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"metadata-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);
        _metadataStore = new MetadataStore();
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, true);
        }
    }

    [Test]
    public void SaveIndex_ValidIndex_ShouldCreateJsonFile()
    {
        // Arrange
        var index = CreateTestIndex();
        var filePath = Path.Combine(_testDirectory, "test-index.json");

        // Act
        _metadataStore.SaveIndex(index, filePath);

        // Assert
        Assert.That(File.Exists(filePath), Is.True);
        
        var jsonContent = File.ReadAllText(filePath);
        var deserializedIndex = JsonSerializer.Deserialize<PhotoIndex>(jsonContent);
        
        Assert.That(deserializedIndex, Is.Not.Null);
        Assert.That(deserializedIndex.Version, Is.EqualTo("1.0.0"));
        Assert.That(deserializedIndex.Photos.Count, Is.EqualTo(1));
    }

    [Test]
    public void LoadIndex_ExistingFile_ShouldReturnIndex()
    {
        // Arrange
        var originalIndex = CreateTestIndex();
        var filePath = Path.Combine(_testDirectory, "test-index.json");
        _metadataStore.SaveIndex(originalIndex, filePath);

        // Act
        var loadedIndex = _metadataStore.LoadIndex(filePath);

        // Assert
        Assert.That(loadedIndex.Version, Is.EqualTo(originalIndex.Version));
        Assert.That(loadedIndex.Photos.Count, Is.EqualTo(originalIndex.Photos.Count));
        Assert.That(loadedIndex.Photos.First().FileName, Is.EqualTo("test.jpg"));
    }

    [Test]
    public void LoadIndex_NonExistentFile_ShouldThrowFileNotFoundException()
    {
        // Arrange
        var nonExistentPath = Path.Combine(_testDirectory, "does-not-exist.json");

        // Act & Assert
        Assert.Throws<FileNotFoundException>(() => _metadataStore.LoadIndex(nonExistentPath));
    }

    [Test]
    public void LoadIndex_InvalidJson_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var filePath = Path.Combine(_testDirectory, "invalid.json");
        File.WriteAllText(filePath, "{ invalid json }");

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => _metadataStore.LoadIndex(filePath));
    }

    [Test]
    public void MetadataExists_ExistingFile_ShouldReturnTrue()
    {
        // Arrange
        var index = CreateTestIndex();
        var filePath = Path.Combine(_testDirectory, "test-index.json");
        _metadataStore.SaveIndex(index, filePath);

        // Act
        var result = _metadataStore.MetadataExists(filePath);

        // Assert
        Assert.That(result, Is.True);
    }

    [Test]
    public void MetadataExists_NonExistentFile_ShouldReturnFalse()
    {
        // Arrange
        var nonExistentPath = Path.Combine(_testDirectory, "does-not-exist.json");

        // Act
        var result = _metadataStore.MetadataExists(nonExistentPath);

        // Assert
        Assert.That(result, Is.False);
    }

    [Test]
    public void UpdatePhotoTransferStatus_ExistingPhoto_ShouldUpdateStatus()
    {
        // Arrange
        var index = CreateTestIndex();
        var filePath = Path.Combine(_testDirectory, "test-index.json");
        _metadataStore.SaveIndex(index, filePath);
        var photoHash = index.Photos.First().Hash;

        // Act
        _metadataStore.UpdatePhotoTransferStatus(filePath, photoHash, "/target/path.jpg");

        // Assert
        var updatedIndex = _metadataStore.LoadIndex(filePath);
        var updatedPhoto = updatedIndex.Photos.First(p => p.Hash == photoHash);
        
        Assert.That(updatedPhoto.IsTransferred, Is.True);
        Assert.That(updatedPhoto.TransferredTo, Is.EqualTo("/target/path.jpg"));
    }

    private PhotoIndex CreateTestIndex()
    {
        return new PhotoIndex
        {
            IndexedAt = DateTime.UtcNow,
            WorkingDirectory = _testDirectory,
            Version = "1.0.0",
            TotalCount = 1,
            SupportedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".tiff", ".tif", ".cr3", ".crw", ".cr2", ".avi", ".mp4" },
            Photos = new List<PhotoMetadata>
            {
                new PhotoMetadata
                {
                    FilePath = Path.Combine(_testDirectory, "test.jpg"),
                    FileName = "test.jpg",
                    Extension = ".jpg",
                    FileSize = 1024,
                    Hash = "testhash123",
                    CreationDate = DateTime.Now,
                    IsTransferred = false
                }
            }
        };
    }
}