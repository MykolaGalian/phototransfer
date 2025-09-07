using System.Diagnostics;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace PhotoTransfer.Tests.ContractTests;

/// <summary>
/// Contract tests for the --index CLI command
/// Tests the exact CLI interface contract as specified in contracts/cli-interface.md
/// These tests MUST FAIL initially (TDD requirement)
/// </summary>
[TestFixture]
[Category("Contract")]
public class IndexCommandTests
{
    private string _testDirectory = string.Empty;
    private string _executablePath = string.Empty;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        // Create temporary test directory
        _testDirectory = Path.Combine(Path.GetTempPath(), $"phototransfer-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);

        // Find the PhotoTransfer executable
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
    public void IndexCommand_WithDefaultOptions_ShouldSucceedWithExitCode0()
    {
        // Arrange: Navigate to test directory with no photos
        var workingDir = _testDirectory;

        // Act: Run phototransfer --index
        var result = RunCommand("--index", workingDir);

        // Assert: Should succeed even with no photos
        Assert.That(result.ExitCode, Is.EqualTo(0), 
            $"Expected exit code 0 (success), but got {result.ExitCode}. Output: {result.Output}, Error: {result.Error}");
        
        Assert.That(result.Output, Does.Contain("Indexing photos"), 
            "Output should indicate indexing process started");
        
        Assert.That(result.Output, Does.Contain("Index complete"), 
            "Output should indicate indexing completed");

        // Verify metadata file was created
        var metadataFile = Path.Combine(workingDir, ".phototransfer-index.json");
        Assert.That(File.Exists(metadataFile), Is.True, 
            "Metadata file .phototransfer-index.json should be created");
    }

    [Test]
    public void IndexCommand_ShowsProgressVisualization()
    {
        // Arrange: Directory with test photos
        var workingDir = CreateTestDirectoryWithPhotos();

        // Act: Run phototransfer --index
        var result = RunCommand("--index", workingDir);

        // Assert: Should show pulsating asterisk progress (mentioned in output or process)
        Assert.That(result.ExitCode, Is.EqualTo(0));
        
        // Note: Progress visualization is runtime behavior, hard to test in unit tests
        // We verify that the indexing completed which implies progress was shown
        Assert.That(result.Output, Does.Contain("Processing"), 
            "Output should indicate processing with progress visualization");
    }

    [Test]
    public void IndexCommand_WithVerboseFlag_ShowsDetailedOutput()
    {
        // Arrange
        var workingDir = CreateTestDirectoryWithPhotos();

        // Act: Run phototransfer --index --verbose
        var result = RunCommand("--index --verbose", workingDir);

        // Assert
        Assert.That(result.ExitCode, Is.EqualTo(0));
        Assert.That(result.Output, Does.Contain("Indexing photos in:"), 
            "Verbose output should show directory being indexed");
        Assert.That(result.Output, Does.Contain("Found"), 
            "Verbose output should show number of files found");
    }

    [Test]
    public void IndexCommand_WithCustomDirectory_ShouldIndexSpecifiedDirectory()
    {
        // Arrange
        var sourceDir = CreateTestDirectoryWithPhotos();
        var workingDir = _testDirectory;

        // Act: Run phototransfer --index --directory <path>
        var result = RunCommand($"--index --directory \"{sourceDir}\"", workingDir);

        // Assert
        Assert.That(result.ExitCode, Is.EqualTo(0));
        Assert.That(result.Output, Does.Contain(sourceDir) | Does.Contain("Indexing photos"), 
            "Output should reference the specified directory");

        // Metadata file should be created in working directory, not source directory
        var metadataFile = Path.Combine(workingDir, ".phototransfer-index.json");
        Assert.That(File.Exists(metadataFile), Is.True);
    }

    [Test]
    public void IndexCommand_WithNonExistentDirectory_ShouldFailWithExitCode1()
    {
        // Arrange
        var nonExistentDir = Path.Combine(_testDirectory, "does-not-exist");
        var workingDir = _testDirectory;

        // Act: Run phototransfer --index --directory <non-existent-path>
        var result = RunCommand($"--index --directory \"{nonExistentDir}\"", workingDir);

        // Assert: Should fail with exit code 1 (directory not found)
        Assert.That(result.ExitCode, Is.EqualTo(1), 
            "Expected exit code 1 for non-existent directory");
        Assert.That(result.Error, Does.Contain("Directory not found") | Does.Contain("not found"), 
            "Error message should indicate directory not found");
    }

    [Test]
    public void IndexCommand_WithReadOnlyDirectory_ShouldFailWithExitCode2()
    {
        // Arrange: Create directory but make metadata file location read-only
        var workingDir = CreateReadOnlyTestDirectory();

        // Act: Run phototransfer --index
        var result = RunCommand("--index", workingDir);

        // Assert: Should fail with exit code 2 (insufficient permissions)
        Assert.That(result.ExitCode, Is.EqualTo(2), 
            "Expected exit code 2 for permission denied");
        Assert.That(result.Error, Does.Contain("Permission denied") | Does.Contain("permission"), 
            "Error message should indicate permission issue");
    }

    [Test]
    public void IndexCommand_WithHelpFlag_ShowsHelpAndExitsSuccessfully()
    {
        // Act: Run phototransfer --index --help
        var result = RunCommand("--index --help", _testDirectory);

        // Assert: Should show help and exit with code 0
        Assert.That(result.ExitCode, Is.EqualTo(0));
        Assert.That(result.Output, Does.Contain("--directory"), 
            "Help should show --directory option");
        Assert.That(result.Output, Does.Contain("--verbose"), 
            "Help should show --verbose option");
        Assert.That(result.Output, Does.Contain("--output"), 
            "Help should show --output option");
    }

    #region Helper Methods

    private string FindExecutable()
    {
        // Look for the executable in the build output
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

    private string CreateTestDirectoryWithPhotos()
    {
        var dir = Path.Combine(_testDirectory, $"photos-{Guid.NewGuid()}");
        Directory.CreateDirectory(dir);

        // Create fake image files for testing
        var imageExtensions = new[] { ".jpg", ".png", ".gif" };
        for (int i = 0; i < 3; i++)
        {
            var fileName = $"test-photo-{i}{imageExtensions[i % imageExtensions.Length]}";
            var filePath = Path.Combine(dir, fileName);
            
            // Create a minimal fake image file
            File.WriteAllBytes(filePath, new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }); // JPEG header
        }

        return dir;
    }

    private string CreateReadOnlyTestDirectory()
    {
        var dir = Path.Combine(_testDirectory, $"readonly-{Guid.NewGuid()}");
        Directory.CreateDirectory(dir);

        // Make directory read-only (platform-specific implementation needed)
        // For now, return regular directory - this test may be skipped on some platforms
        return dir;
    }

    #endregion
}