using System.CommandLine;
using PhotoTransfer.Models;
using PhotoTransfer.Services;
using PhotoTransfer.Utilities;

namespace PhotoTransfer.Commands;

public static class IndexCommand
{
    public static Command Create()
    {
        var directoryOption = new Option<string?>(
            "--directory",
            "Directory or directories to index separated by comma (defaults to current directory)")
        {
            ArgumentHelpName = "path1,path2,..."
        };

        var verboseOption = new Option<bool>(
            "--verbose", 
            "Show detailed output");

        var outputOption = new Option<string?>(
            "--output",
            "Output file for metadata (defaults to .phototransfer-index.json)")
        {
            ArgumentHelpName = "file"
        };

        var statOption = new Option<bool>(
            "--stat",
            "Show statistics table grouped by year-month");

        var updateBaseOption = new Option<bool>(
            "--update-base",
            "Recreate base-index with new formats but preserve existing metadata");

        var command = new Command("--index", "Index photos in directory")
        {
            directoryOption,
            verboseOption,
            outputOption,
            statOption,
            updateBaseOption
        };

        command.SetHandler(async (directory, verbose, output, stat, updateBase) =>
        {
            await ExecuteIndexCommand(directory, verbose, output, stat, updateBase);
        }, directoryOption, verboseOption, outputOption, statOption, updateBaseOption);

        return command;
    }

    private static async Task ExecuteIndexCommand(string? directory, bool verbose, string? output, bool stat, bool updateBase)
    {
        try
        {
            // Parse multiple directories separated by comma
            var directoryInput = directory ?? Environment.CurrentDirectory;
            var targetDirectories = directoryInput
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();

            // Validate all directories exist
            foreach (var dir in targetDirectories)
            {
                if (!Directory.Exists(dir))
                {
                    Console.Error.WriteLine($"Error: Directory not found: {dir}");
                    Environment.Exit(1);
                    return;
                }
            }

            // Check write permissions for metadata file
            var metadataFile = output ?? Path.Combine(Environment.CurrentDirectory, ".phototransfer-index.json");
            var metadataDir = Path.GetDirectoryName(metadataFile) ?? Environment.CurrentDirectory;
            
            if (!Directory.Exists(metadataDir) || !HasWritePermission(metadataDir))
            {
                Console.Error.WriteLine("Error: Permission denied - Cannot write metadata file to current directory");
                Environment.Exit(2);
                return;
            }

            if (verbose)
            {
                Console.WriteLine($"Indexing photos in {targetDirectories.Count} director{(targetDirectories.Count == 1 ? "y" : "ies")}:");
                foreach (var dir in targetDirectories)
                {
                    Console.WriteLine($"  - {dir}");
                }
            }

            Console.WriteLine("Indexing photos...");

            // Perform indexing with progress callback
            var indexer = new PhotoIndexer();
            var index = await Task.Run(() => indexer.IndexDirectories(targetDirectories, metadataFile, updateBase,
                message => Console.WriteLine(message)));
            Console.WriteLine("Processing done.");

                if (verbose)
                {
                    Console.WriteLine($"Found {index.Photos.Count} photos");
                    foreach (var photo in index.Photos.Take(10)) // Show first 10 in verbose mode
                    {
                        Console.WriteLine($"  {photo.FileName} ({photo.Extension}) - {photo.EffectiveDate:yyyy-MM-dd}");
                    }
                    if (index.Photos.Count > 10)
                    {
                        Console.WriteLine($"  ... and {index.Photos.Count - 10} more");
                    }
                }

                // Save metadata with incremental naming
                var metadataStore = new MetadataStore();
                var incrementalFile = metadataStore.GetNextIncrementalFilePath(metadataFile);
                metadataStore.SaveIndex(index, incrementalFile);

                Console.WriteLine($"Index complete - {index.Photos.Count} photos indexed");
                if (verbose)
                {
                    Console.WriteLine($"Metadata saved to: {incrementalFile}");
                }

                if (stat)
                {
                    ShowStatistics(index);
                }

                Environment.Exit(0);
        }
        catch (DirectoryNotFoundException)
        {
            Console.Error.WriteLine("Error: Directory not found");
            Environment.Exit(1);
        }
        catch (UnauthorizedAccessException)
        {
            Console.Error.WriteLine("Error: Permission denied");
            Environment.Exit(2);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            Environment.Exit(3);
        }
    }

    private static void ShowStatistics(PhotoIndex index)
    {
        Console.WriteLine();
        Console.WriteLine("Statistics by Period:");
        Console.WriteLine("====================");
        
        var statistics = index.Photos
            .GroupBy(photo => new { Year = photo.EffectiveDate.Year, Month = photo.EffectiveDate.Month })
            .Select(group => new 
            {
                Date = $"{group.Key.Year:0000}-{group.Key.Month:00}",
                Amount = group.Count()
            })
            .OrderBy(stat => stat.Date)
            .ToList();

        if (!statistics.Any())
        {
            Console.WriteLine("No photos found.");
            return;
        }

        Console.WriteLine($"{"Date",-10} | {"Amount",8}");
        Console.WriteLine(new string('-', 21));
        
        foreach (var stat in statistics)
        {
            Console.WriteLine($"{stat.Date,-10} | {stat.Amount,8}");
        }
        
        Console.WriteLine(new string('-', 21));
        Console.WriteLine($"{"Total",-10} | {statistics.Sum(s => s.Amount),8}");
    }

    private static bool HasWritePermission(string directoryPath)
    {
        try
        {
            var testFile = Path.Combine(directoryPath, $"test_{Guid.NewGuid()}.tmp");
            File.WriteAllText(testFile, "test");
            File.Delete(testFile);
            return true;
        }
        catch
        {
            return false;
        }
    }
}