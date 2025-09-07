using System.CommandLine;

namespace PhotoTransfer.Commands;

public static class TypesCommand
{
    public static Command Create()
    {
        var directoryOption = new Option<string?>(
            "--directory",
            "Directory to analyze (defaults to current directory)")
        {
            ArgumentHelpName = "path"
        };

        var command = new Command("--types", "Show statistics of all file types in directory")
        {
            directoryOption
        };

        command.SetHandler(async (directory) =>
        {
            await ExecuteTypesCommand(directory);
        }, directoryOption);

        return command;
    }

    private static async Task ExecuteTypesCommand(string? directory)
    {
        try
        {
            // Default to current directory if not specified
            var targetDirectory = directory ?? Environment.CurrentDirectory;
            
            // Validate directory exists
            if (!Directory.Exists(targetDirectory))
            {
                Console.Error.WriteLine($"Error: Directory not found: {targetDirectory}");
                Environment.Exit(1);
                return;
            }

            Console.WriteLine($"Analyzing file types in: {targetDirectory}");
            Console.WriteLine("Scanning all files and subdirectories...");

            // Collect file type statistics
            var statistics = await Task.Run(() => CollectFileTypeStatistics(targetDirectory));

            DisplayFileTypeStatistics(statistics);

            Environment.Exit(0);
        }
        catch (UnauthorizedAccessException)
        {
            Console.Error.WriteLine("Error: Permission denied - Cannot access directory or some files");
            Environment.Exit(2);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            Environment.Exit(3);
        }
    }

    private static Dictionary<string, int> CollectFileTypeStatistics(string directoryPath)
    {
        var statistics = new Dictionary<string, int>();
        var allFiles = Directory.GetFiles(directoryPath, "*", SearchOption.AllDirectories);

        foreach (var filePath in allFiles)
        {
            try
            {
                var extension = Path.GetExtension(filePath).ToLowerInvariant();
                
                // Handle files without extension
                if (string.IsNullOrEmpty(extension))
                {
                    extension = "[no extension]";
                }

                // Count the extension
                if (statistics.ContainsKey(extension))
                {
                    statistics[extension]++;
                }
                else
                {
                    statistics[extension] = 1;
                }
            }
            catch
            {
                // Skip files that can't be accessed
                continue;
            }
        }

        return statistics;
    }

    private static void DisplayFileTypeStatistics(Dictionary<string, int> statistics)
    {
        if (!statistics.Any())
        {
            Console.WriteLine("No files found.");
            return;
        }

        Console.WriteLine();
        Console.WriteLine("File Type Statistics:");
        Console.WriteLine("====================");

        // Sort by count (descending) then by extension (ascending)
        var sortedStatistics = statistics
            .OrderByDescending(kvp => kvp.Value)
            .ThenBy(kvp => kvp.Key)
            .ToList();

        // Find the longest extension name for formatting
        var maxExtensionLength = sortedStatistics.Max(s => s.Key.Length);
        var columnWidth = Math.Max(maxExtensionLength, 10); // Minimum width of 10

        var headerSeparator = string.Concat(Enumerable.Repeat('-', columnWidth + 11));
        
        Console.WriteLine($"{"Type".PadRight(columnWidth)} | {"Amount",8}");
        Console.WriteLine(headerSeparator);
        
        foreach (var stat in sortedStatistics)
        {
            Console.WriteLine($"{stat.Key.PadRight(columnWidth)} | {stat.Value,8}");
        }
        
        Console.WriteLine(headerSeparator);
        Console.WriteLine($"{"Total".PadRight(columnWidth)} | {statistics.Values.Sum(),8}");
        
        Console.WriteLine();
        Console.WriteLine($"Found {statistics.Count} different file types");
        Console.WriteLine($"Total files analyzed: {statistics.Values.Sum()}");
    }
}