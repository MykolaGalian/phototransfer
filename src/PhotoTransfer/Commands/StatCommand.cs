using System.CommandLine;
using PhotoTransfer.Models;
using PhotoTransfer.Services;

namespace PhotoTransfer.Commands;

public static class StatCommand
{
    public static Command Create()
    {
        var inputOption = new Option<string?>(
            "--input",
            "Metadata file to read statistics from (defaults to .phototransfer-index.json)")
        {
            ArgumentHelpName = "file"
        };

        var command = new Command("--stat", "Show statistics table grouped by year-month")
        {
            inputOption
        };

        command.SetHandler(async (input) =>
        {
            await ExecuteStatCommand(input);
        }, inputOption);

        return command;
    }

    private static async Task ExecuteStatCommand(string? input)
    {
        try
        {
            // Default to current directory metadata file if not specified
            var basePath = input ?? Path.Combine(Environment.CurrentDirectory, ".phototransfer-index.json");
            var metadataStore = new MetadataStore();
            var metadataFile = metadataStore.GetLatestIndexFile(basePath);
            
            if (!File.Exists(metadataFile))
            {
                Console.Error.WriteLine($"Error: No index files found. Looking for: {basePath}");
                Console.Error.WriteLine("Please run indexing first with: phototransfer --index");
                Environment.Exit(1);
                return;
            }

            Console.WriteLine($"Reading statistics from: {Path.GetFileName(metadataFile)}");

            // Load the index
            var index = await Task.Run(() => metadataStore.LoadIndex(metadataFile));

            ShowStatistics(index);

            Environment.Exit(0);
        }
        catch (FileNotFoundException)
        {
            Console.Error.WriteLine("Error: Metadata file not found");
            Console.Error.WriteLine("Please run indexing first with: phototransfer --index");
            Environment.Exit(1);
        }
        catch (InvalidOperationException ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
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
            Console.WriteLine("No photos found in index.");
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
        
        Console.WriteLine();
        Console.WriteLine($"Index created: {index.IndexedAt:yyyy-MM-dd HH:mm:ss}");
        Console.WriteLine($"Working directory: {index.WorkingDirectory}");
    }
}