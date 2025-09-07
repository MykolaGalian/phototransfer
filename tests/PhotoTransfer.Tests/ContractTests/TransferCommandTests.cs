using System.Diagnostics;
using System.Text.Json;
using NUnit.Framework;

namespace PhotoTransfer.Tests.ContractTests;

/// <summary>
/// Contract tests for the --YYYY-MM transfer CLI command
/// Tests the exact CLI interface contract as specified in contracts/cli-interface.md
/// These tests MUST FAIL initially (TDD requirement)
/// </summary>
[TestFixture]
[Category("Contract")]
public class TransferCommandTests
{
    private string _testDirectory = string.Empty;
    private string _executablePath = string.Empty;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"phototransfer-transfer-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);
        _executablePath = FindExecutable();
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, true);
        }
    }

    [Test]
    public void TransferCommand_WithValidDate_ShouldSucceedWithExitCode0()
    {
        // Arrange: Create metadata file with photos from 2012-01
        var workingDir = SetupTestDirectoryWithMetadata();

        // Act: Run phototransfer --2012-01
        var result = RunCommand("--2012-01", workingDir);

        // Assert: Should succeed with exit code 0
        Assert.That(result.ExitCode, Is.EqualTo(0), 
            $"Expected exit code 0 (success), but got {result.ExitCode}. Output: {result.Output}, Error: {result.Error}");

        Assert.That(result.Output, Does.Contain("Loading metadata from"), 
            "Output should indicate metadata loading");
        
        Assert.That(result.Output, Does.Contain("Found") & Does.Contain("photos for period: 2012-01"), 
            "Output should show number of photos found for the period");

        Assert.That(result.Output, Does.Contain("Transfer complete"), 
            "Output should indicate transfer completion");

        // Verify target directory was created
        var targetDir = Path.Combine(workingDir, "phototransfer", "2012-01");
        Assert.That(Directory.Exists(targetDir), Is.True, 
            "Target directory phototransfer/2012-01 should be created");
    }

    [Test]
    public void TransferCommand_WithDuplicates_ShouldHandleNumericSuffixes()
    {
        // Arrange: Setup with duplicate filenames for same period
        var workingDir = SetupTestDirectoryWithDuplicates();

        // Act: Run phototransfer --2023-06
        var result = RunCommand("--2023-06", workingDir);

        // Assert: Should succeed and show duplicate handling
        Assert.That(result.ExitCode, Is.EqualTo(0));
        
        Assert.That(result.Output, Does.Contain("(duplicate)") | Does.Contain("(0)"), 
            "Output should indicate duplicate handling");

        // Verify both files exist with numeric suffixes
        var targetDir = Path.Combine(workingDir, "phototransfer", "2023-06");
        var originalFile = Path.Combine(targetDir, "photo.jpg");
        var duplicateFile = Path.Combine(targetDir, "photo(0).jpg");

        Assert.That(File.Exists(originalFile) || File.Exists(duplicateFile), Is.True,
            "Either original or duplicate file should exist");
    }

    [Test]
    public void TransferCommand_WithCopyFlag_ShouldCopyInsteadOfMove()
    {
        // Arrange
        var workingDir = SetupTestDirectoryWithMetadata();
        var originalPhoto = Path.Combine(workingDir, "original", "photo-2012-01.jpg");
        File.WriteAllBytes(originalPhoto, new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 });

        // Act: Run phototransfer --2012-01 --copy
        var result = RunCommand("--2012-01 --copy", workingDir);

        // Assert: Should succeed and preserve original files
        Assert.That(result.ExitCode, Is.EqualTo(0));
        Assert.That(File.Exists(originalPhoto), Is.True, 
            "Original file should still exist when using --copy flag");

        var targetDir = Path.Combine(workingDir, "phototransfer", "2012-01");
        Assert.That(Directory.Exists(targetDir), Is.True, 
            "Target directory should still be created");
    }

    [Test]
    public void TransferCommand_WithDryRun_ShouldShowWhatWouldBeTransferred()
    {
        // Arrange
        var workingDir = SetupTestDirectoryWithMetadata();

        // Act: Run phototransfer --2012-01 --dry-run
        var result = RunCommand("--2012-01 --dry-run", workingDir);

        // Assert: Should show what would be transferred without moving files
        Assert.That(result.ExitCode, Is.EqualTo(0));
        Assert.That(result.Output, Does.Contain("Would transfer") | Does.Contain("dry run"), 
            "Output should indicate dry run mode");

        // Target directory should NOT be created in dry run
        var targetDir = Path.Combine(workingDir, "phototransfer", "2012-01");
        Assert.That(Directory.Exists(targetDir), Is.False, 
            "Target directory should not be created in dry run mode");
    }

    [Test]
    public void TransferCommand_WithCustomTarget_ShouldUseSpecifiedDirectory()
    {
        // Arrange
        var workingDir = SetupTestDirectoryWithMetadata();
        var customTarget = Path.Combine(workingDir, "custom-output");

        // Act: Run phototransfer --2012-01 --target <custom-path>
        var result = RunCommand($"--2012-01 --target \"{customTarget}\"", workingDir);

        // Assert: Should use custom target directory
        Assert.That(result.ExitCode, Is.EqualTo(0));

        var targetDir = Path.Combine(customTarget, "2012-01");
        Assert.That(Directory.Exists(targetDir), Is.True, 
            "Custom target directory should be created");
    }

    [Test]
    public void TransferCommand_WithMissingMetadata_ShouldFailWithExitCode1()
    {
        // Arrange: Directory with no metadata file
        var workingDir = _testDirectory;

        // Act: Run phototransfer --2012-01
        var result = RunCommand("--2012-01", workingDir);

        // Assert: Should fail with exit code 1 (metadata file not found)
        Assert.That(result.ExitCode, Is.EqualTo(1), 
            "Expected exit code 1 for missing metadata file");
        Assert.That(result.Error, Does.Contain("Metadata file not found") | Does.Contain("not found"), 
            "Error message should indicate metadata file not found");
    }

    [Test]
    public void TransferCommand_WithNoPhotosForPeriod_ShouldFailWithExitCode2()
    {
        // Arrange: Metadata file with no photos for requested period
        var workingDir = SetupTestDirectoryWithMetadata();

        // Act: Run phototransfer --1995-01 (no photos from 1995)
        var result = RunCommand("--1995-01", workingDir);

        // Assert: Should fail with exit code 2 (no photos found)
        Assert.That(result.ExitCode, Is.EqualTo(2), 
            "Expected exit code 2 for no photos found");
        Assert.That(result.Error + result.Output, Does.Contain("No photos found for period"),
            "Error/output should indicate no photos found for period");
    }

    [Test]
    public void TransferCommand_WithInvalidDateFormat_ShouldFailWithExitCode2()
    {
        // Act: Run with invalid date formats
        var invalidFormats = new[] { "--2012-13", "--12-01", "--2012-1", "--invalid" };

        foreach (var format in invalidFormats)
        {
            var result = RunCommand(format, _testDirectory);
            
            // Assert: Should fail with exit code 2 (invalid format)
            Assert.That(result.ExitCode, Is.Not.EqualTo(0), 
                $"Command '{format}' should fail with non-zero exit code");
        }
    }

    [Test]
    public void TransferCommand_WithVerbose_ShowsDetailedProgress()
    {
        // Arrange
        var workingDir = SetupTestDirectoryWithMetadata();

        // Act: Run phototransfer --2012-01 --verbose
        var result = RunCommand("--2012-01 --verbose", workingDir);

        // Assert: Should show detailed transfer information
        Assert.That(result.ExitCode, Is.EqualTo(0));
        Assert.That(result.Output, Does.Contain("Transferring:"), 
            "Verbose output should show individual file transfers");
        Assert.That(result.Output, Does.Contain("->"), 
            "Verbose output should show source -> destination format");
    }

    [Test]
    public void TransferCommand_WithHelp_ShowsCommandHelp()
    {
        // Act: Run phototransfer --2012-01 --help
        var result = RunCommand("--2012-01 --help", _testDirectory);

        // Assert: Should show help and exit successfully
        Assert.That(result.ExitCode, Is.EqualTo(0));
        Assert.That(result.Output, Does.Contain("--copy"), 
            "Help should show --copy option");
        Assert.That(result.Output, Does.Contain("--dry-run"), 
            "Help should show --dry-run option");
        Assert.That(result.Output, Does.Contain("--target"), 
            "Help should show --target option");
    }

    #region Helper Methods

    private string FindExecutable()
    {
        var possiblePaths = new[]
        {
            "src/PhotoTransfer/bin/Debug/net9.0/phototransfer.exe",
            "src/PhotoTransfer/bin/Debug/net9.0/phototransfer",
            "src/PhotoTransfer/bin/Release/net9.0/phototransfer.exe",
            "src/PhotoTransfer/bin/Release/net9.0/phototransfer",
            "publish/win-x64/phototransfer.exe",
            "publish/linux-x64/phototransfer",
            "publish/osx-x64/phototransfer"
        };

        foreach (var path in possiblePaths)
        {
            var fullPath = Path.GetFullPath(path);
            if (File.Exists(fullPath))
            {
                return fullPath;
            }
        }

        throw new FileNotFoundException("PhotoTransfer executable not found. Build the project first.");
    }

    private (int ExitCode, string Output, string Error) RunCommand(string arguments, string workingDirectory)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = _executablePath,
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(startInfo);
        if (process == null)
        {
            throw new InvalidOperationException("Failed to start PhotoTransfer process");
        }

        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();

        return (process.ExitCode, output, error);
    }

    private string SetupTestDirectoryWithMetadata()
    {
        var dir = Path.Combine(_testDirectory, $"with-metadata-{Guid.NewGuid()}");
        Directory.CreateDirectory(dir);

        // Create original photos directory
        var originalDir = Path.Combine(dir, "original");
        Directory.CreateDirectory(originalDir);

        // Create fake metadata file with photos from 2012-01
        var metadata = new
        {
            indexedAt = DateTime.UtcNow,
            workingDirectory = originalDir,
            version = "1.0.0",
            totalCount = 2,
            supportedExtensions = new[] { ".jpg", ".png" },
            photos = new[]
            {
                new
                {
                    filePath = Path.Combine(originalDir, "photo1.jpg"),
                    fileName = "photo1.jpg",
                    creationDate = new DateTime(2012, 1, 15),
                    fileSize = 1024L,
                    extension = ".jpg",
                    hash = "abc123",
                    isTransferred = false,
                    transferredTo = (string?)null
                },
                new
                {
                    filePath = Path.Combine(originalDir, "photo2.png"),
                    fileName = "photo2.png", 
                    creationDate = new DateTime(2012, 1, 20),
                    fileSize = 2048L,
                    extension = ".png",
                    hash = "def456",
                    isTransferred = false,
                    transferredTo = (string?)null
                }
            }
        };

        var metadataJson = JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true });
        var metadataPath = Path.Combine(dir, ".phototransfer-index.json");
        File.WriteAllText(metadataPath, metadataJson);

        // Create the actual photo files referenced in metadata
        File.WriteAllBytes(Path.Combine(originalDir, "photo1.jpg"), new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 });
        File.WriteAllBytes(Path.Combine(originalDir, "photo2.png"), new byte[] { 0x89, 0x50, 0x4E, 0x47 });

        return dir;
    }

    private string SetupTestDirectoryWithDuplicates()
    {
        var dir = Path.Combine(_testDirectory, $"with-duplicates-{Guid.NewGuid()}");
        Directory.CreateDirectory(dir);

        var originalDir = Path.Combine(dir, "original");
        Directory.CreateDirectory(originalDir);

        // Create metadata with duplicate filenames
        var metadata = new
        {
            indexedAt = DateTime.UtcNow,
            workingDirectory = originalDir,
            version = "1.0.0",
            totalCount = 2,
            supportedExtensions = new[] { ".jpg" },
            photos = new[]
            {
                new
                {
                    filePath = Path.Combine(originalDir, "subdir1", "photo.jpg"),
                    fileName = "photo.jpg",
                    creationDate = new DateTime(2023, 6, 1),
                    fileSize = 1024L,
                    extension = ".jpg",
                    hash = "abc123",
                    isTransferred = false,
                    transferredTo = (string?)null
                },
                new
                {
                    filePath = Path.Combine(originalDir, "subdir2", "photo.jpg"),
                    fileName = "photo.jpg",
                    creationDate = new DateTime(2023, 6, 15),
                    fileSize = 2048L,
                    extension = ".jpg", 
                    hash = "def456", // Different hash = different content
                    isTransferred = false,
                    transferredTo = (string?)null
                }
            }
        };

        var metadataJson = JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(Path.Combine(dir, ".phototransfer-index.json"), metadataJson);

        // Create the subdirectories and duplicate files
        Directory.CreateDirectory(Path.Combine(originalDir, "subdir1"));
        Directory.CreateDirectory(Path.Combine(originalDir, "subdir2"));
        File.WriteAllBytes(Path.Combine(originalDir, "subdir1", "photo.jpg"), new byte[] { 0xFF, 0xD8, 0x01 });
        File.WriteAllBytes(Path.Combine(originalDir, "subdir2", "photo.jpg"), new byte[] { 0xFF, 0xD8, 0x02 });

        return dir;
    }

    #endregion
}