using NUnit.Framework;
using PhotoTransfer.Models;
using PhotoTransfer.Services;

namespace PhotoTransfer.Tests.UnitTests;

[TestFixture]
[Category("Unit")]
public class PhotoTransferServiceTests
{
    private PhotoTransferService _service = null!;
    private string _testDirectory = string.Empty;

    [SetUp]
    public void SetUp()
    {
        _service = new PhotoTransferService();
        _testDirectory = Path.Combine(Path.GetTempPath(), $"transfer-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);
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
    public void GetPhotosForPeriod_MatchingPhotos_ShouldReturnFilteredList()
    {
        // Arrange
        var index = CreateTestIndex();
        var period = new TimePeriod(2023, 6);

        // Act
        var result = _service.GetPhotosForPeriod(index, period);

        // Assert
        Assert.That(result.Count, Is.EqualTo(2)); // Only June 2023 photos
        Assert.That(result.All(p => period.Contains(p.CreationDate)), Is.True);
    }

    [Test]
    public void GetPhotosForPeriod_NoMatchingPhotos_ShouldReturnEmptyList()
    {
        // Arrange
        var index = CreateTestIndex();
        var period = new TimePeriod(2020, 1); // No photos from this period

        // Act
        var result = _service.GetPhotosForPeriod(index, period);

        // Assert
        Assert.That(result.Count, Is.EqualTo(0));
    }

    [Test]
    public void PlanTransfer_UniqueFilenames_ShouldCreateOperationsWithoutSuffixes()
    {
        // Arrange
        var photos = new List<PhotoMetadata>
        {
            CreateTestPhoto("photo1.jpg"),
            CreateTestPhoto("photo2.jpg")
        };

        // Act
        var operations = _service.PlanTransfer(photos, _testDirectory);

        // Assert
        Assert.That(operations.Count, Is.EqualTo(2));
        Assert.That(operations[0].TargetPath, Is.EqualTo(Path.Combine(_testDirectory, "photo1.jpg")));
        Assert.That(operations[1].TargetPath, Is.EqualTo(Path.Combine(_testDirectory, "photo2.jpg")));
        Assert.That(operations.All(op => op.Type == TransferType.Move), Is.True);
    }

    [Test]
    public void PlanTransfer_DuplicateFilenames_ShouldAddNumericSuffixes()
    {
        // Arrange
        var photos = new List<PhotoMetadata>
        {
            CreateTestPhoto("photo.jpg"),
            CreateTestPhoto("photo.jpg", "hash2")
        };

        // Act
        var operations = _service.PlanTransfer(photos, _testDirectory);

        // Assert
        Assert.That(operations.Count, Is.EqualTo(2));
        Assert.That(operations[0].TargetPath, Is.EqualTo(Path.Combine(_testDirectory, "photo.jpg")));
        Assert.That(operations[1].TargetPath, Is.EqualTo(Path.Combine(_testDirectory, "photo(1).jpg")));
    }

    [Test]
    public void PlanTransfer_WithCopyType_ShouldCreateCopyOperations()
    {
        // Arrange
        var photos = new List<PhotoMetadata> { CreateTestPhoto("photo.jpg") };

        // Act
        var operations = _service.PlanTransfer(photos, _testDirectory, TransferType.Copy);

        // Assert
        Assert.That(operations.Count, Is.EqualTo(1));
        Assert.That(operations[0].Type, Is.EqualTo(TransferType.Copy));
    }

    [Test]
    public void ExecuteTransfer_DryRun_ShouldMarkAsCompletedWithoutFileOperations()
    {
        // Arrange
        var photo = CreateTestPhoto("test.jpg");
        var operations = new List<TransferOperation>
        {
            new TransferOperation(photo, Path.Combine(_testDirectory, "test.jpg"), TransferType.Move)
        };

        // Act
        _service.ExecuteTransfer(operations, dryRun: true);

        // Assert
        Assert.That(operations[0].Status, Is.EqualTo(OperationStatus.Completed));
        Assert.That(File.Exists(operations[0].TargetPath), Is.False); // No actual file operation
    }

    [Test]
    public void ExecuteTransfer_InvalidSourceFile_ShouldMarkAsFailed()
    {
        // Arrange
        var photo = CreateTestPhoto("nonexistent.jpg");
        photo.FilePath = Path.Combine(_testDirectory, "nonexistent.jpg"); // File doesn't exist
        var operations = new List<TransferOperation>
        {
            new TransferOperation(photo, Path.Combine(_testDirectory, "target.jpg"), TransferType.Move)
        };

        // Act
        _service.ExecuteTransfer(operations, dryRun: false);

        // Assert
        Assert.That(operations[0].Status, Is.EqualTo(OperationStatus.Failed));
        Assert.That(operations[0].ErrorMessage, Is.Not.Null.And.Not.Empty);
    }

    private PhotoIndex CreateTestIndex()
    {
        return new PhotoIndex
        {
            Photos = new List<PhotoMetadata>
            {
                CreateTestPhoto("june1.jpg", "hash1", new DateTime(2023, 6, 1)),
                CreateTestPhoto("june2.jpg", "hash2", new DateTime(2023, 6, 15)),
                CreateTestPhoto("july1.jpg", "hash3", new DateTime(2023, 7, 1))
            }
        };
    }

    private PhotoMetadata CreateTestPhoto(string fileName, string? hash = null, DateTime? creationDate = null)
    {
        return new PhotoMetadata
        {
            FilePath = Path.Combine(_testDirectory, fileName),
            FileName = fileName,
            Extension = Path.GetExtension(fileName).ToLowerInvariant(),
            FileSize = 1024,
            Hash = hash ?? $"hash-{fileName}",
            CreationDate = creationDate ?? DateTime.Now,
            IsTransferred = false
        };
    }
}