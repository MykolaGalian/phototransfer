using System.CommandLine;
using System.Text.RegularExpressions;
using PhotoTransfer.Models;
using PhotoTransfer.Services;

namespace PhotoTransfer.Commands;

public static class TransferCommand
{
    public static Command Create()
    {
        // Create a command that accepts date patterns like --2012-01
        var dateArgument = new Argument<string?>(
            "period",
            "Date period in format YYYY-MM (optional when using --all)")
        {
            Arity = ArgumentArity.ZeroOrOne
        };

        var copyOption = new Option<bool>(
            "--copy",
            "Copy files instead of moving them");

        var dryRunOption = new Option<bool>(
            "--dry-run",
            "Show what would be transferred without moving files");

        var targetOption = new Option<string?>(
            "--target",
            "Target directory (defaults to ./phototransfer)")
        {
            ArgumentHelpName = "directory"
        };

        var verboseOption = new Option<bool>(
            "--verbose",
            "Show detailed transfer information");

        var allOption = new Option<bool>(
            "--all",
            "Transfer all photos organized by their monthly periods");

        var command = new Command("--transfer", "Transfer photos from specified period or all periods")
        {
            dateArgument,
            copyOption,
            dryRunOption,
            targetOption,
            verboseOption,
            allOption
        };

        command.SetHandler(async (period, copy, dryRun, target, verbose, all) =>
        {
            await ExecuteTransferCommand(period, copy, dryRun, target, verbose, all);
        }, dateArgument, copyOption, dryRunOption, targetOption, verboseOption, allOption);

        return command;
    }

    // Create additional commands for date patterns like --2012-01
    public static List<Command> CreateDateCommands()
    {
        var commands = new List<Command>();
        
        // This is a placeholder - in real implementation we'd need to handle
        // dynamic date pattern recognition at runtime
        return commands;
    }

    private static async Task ExecuteTransferCommand(string? period, bool copy, bool dryRun, string? target, bool verbose, bool all)
    {
        try
        {
            // Validate arguments
            if (!all && string.IsNullOrEmpty(period))
            {
                Console.Error.WriteLine("Error: Either specify a period (YYYY-MM) or use --all flag");
                Environment.Exit(2);
                return;
            }

            if (all && !string.IsNullOrEmpty(period))
            {
                Console.Error.WriteLine("Error: Cannot specify both period and --all flag");
                Environment.Exit(2);
                return;
            }

            // Check for metadata file and get latest
            var basePath = Path.Combine(Environment.CurrentDirectory, ".phototransfer-index.json");
            var metadataStore = new MetadataStore();
            var metadataFile = metadataStore.GetLatestIndexFile(basePath);
            
            if (!File.Exists(metadataFile))
            {
                Console.Error.WriteLine("Error: No index files found. Run --index first to create photo index.");
                Environment.Exit(1);
                return;
            }

            if (verbose)
            {
                Console.WriteLine($"Loading metadata from: {Path.GetFileName(metadataFile)}");
            }

            // Load metadata
            var index = metadataStore.LoadIndex(metadataFile);
            var transferService = new PhotoTransferService(metadataStore);

            if (all)
            {
                ExecuteTransferAllPeriods(index, transferService, copy, dryRun, target, verbose, metadataFile);
            }
            else
            {
                ExecuteTransferSinglePeriod(period!, index, transferService, copy, dryRun, target, verbose, metadataFile);
            }
        }
        catch (FileNotFoundException)
        {
            Console.Error.WriteLine("Error: Metadata file not found. Run --index first to create photo index.");
            Environment.Exit(1);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Invalid metadata"))
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            Environment.Exit(1);
        }
        catch (UnauthorizedAccessException)
        {
            Console.Error.WriteLine("Error: Permission denied - Cannot access files or directories.");
            Environment.Exit(2);
        }
        catch (DirectoryNotFoundException)
        {
            Console.Error.WriteLine("Error: Directory not found.");
            Environment.Exit(1);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            Environment.Exit(3);
        }
    }

    // Helper method to check if arguments match date pattern
    public static bool IsDatePattern(string arg)
    {
        return Regex.IsMatch(arg, @"^--?\d{4}-\d{2}$");
    }

    // Parse date pattern arguments from command line
    public static async Task<int> HandleDatePattern(string[] args)
    {
        // Find date pattern argument
        var dateArg = args.FirstOrDefault(IsDatePattern);
        if (dateArg == null)
        {
            return 1; // No date pattern found
        }

        // Parse other options
        var copy = args.Contains("--copy");
        var dryRun = args.Contains("--dry-run");
        var verbose = args.Contains("--verbose");
        
        string? target = null;
        var targetIndex = Array.IndexOf(args, "--target");
        if (targetIndex >= 0 && targetIndex + 1 < args.Length)
        {
            target = args[targetIndex + 1];
        }

        // Remove leading dashes and execute transfer
        var period = dateArg.TrimStart('-');
        await ExecuteTransferCommand(period, copy, dryRun, target, verbose, false);
        return 0;
    }

    private static void ExecuteTransferSinglePeriod(string period, PhotoIndex index, PhotoTransferService transferService, bool copy, bool dryRun, string? target, bool verbose, string metadataFile)
    {
        // Parse the date period
        TimePeriod timePeriod;
        try
        {
            timePeriod = TimePeriod.Parse(period);
        }
        catch (FormatException)
        {
            Console.Error.WriteLine($"Error: Invalid date format: {period}. Expected format: YYYY-MM");
            Environment.Exit(2);
            return;
        }

        // Find photos for the specified period
        var photosForPeriod = transferService.GetPhotosForPeriod(index, timePeriod);

        if (photosForPeriod.Count == 0)
        {
            Console.Error.WriteLine($"Error: No photos found for period: {timePeriod}");
            Environment.Exit(2);
            return;
        }

        Console.WriteLine($"Found {photosForPeriod.Count} photos for period: {timePeriod}");

        // Determine target directory
        var targetDirectory = target ?? Path.Combine(Environment.CurrentDirectory, "phototransfer");
        var periodTargetDirectory = Path.Combine(targetDirectory, timePeriod.ToString());

        if (verbose)
        {
            Console.WriteLine($"Target directory: {periodTargetDirectory}");
        }

        // Plan and execute transfer for this period
        ExecuteTransferForPeriod(timePeriod, photosForPeriod, periodTargetDirectory, transferService, copy, dryRun, verbose, metadataFile);
    }

    private static void ExecuteTransferAllPeriods(PhotoIndex index, PhotoTransferService transferService, bool copy, bool dryRun, string? target, bool verbose, string metadataFile)
    {
        // Group photos by period and get unique periods
        var periodGroups = index.Photos
            .GroupBy(photo => new { Year = photo.EffectiveDate.Year, Month = photo.EffectiveDate.Month })
            .Select(group => new 
            {
                Period = new TimePeriod(group.Key.Year, group.Key.Month),
                Photos = group.ToList()
            })
            .OrderBy(group => group.Period.Year)
            .ThenBy(group => group.Period.Month)
            .ToList();

        if (!periodGroups.Any())
        {
            Console.Error.WriteLine("Error: No photos found in index");
            Environment.Exit(2);
            return;
        }

        Console.WriteLine($"Found photos in {periodGroups.Count} periods:");
        foreach (var group in periodGroups)
        {
            Console.WriteLine($"  {group.Period}: {group.Photos.Count} photos");
        }
        Console.WriteLine();

        var targetDirectory = target ?? Path.Combine(Environment.CurrentDirectory, "phototransfer");

        // Process each period
        var totalSucceeded = 0;
        var totalFailed = 0;

        foreach (var group in periodGroups)
        {
            var periodTargetDirectory = Path.Combine(targetDirectory, group.Period.ToString());
            
            if (verbose)
            {
                Console.WriteLine($"Processing period: {group.Period}");
                Console.WriteLine($"Target directory: {periodTargetDirectory}");
            }

            // Filter duplicates for this period (same logic as GetPhotosForPeriod but with deduplication)
            var photosForPeriod = group.Photos
                .GroupBy(photo => photo.FileName, StringComparer.OrdinalIgnoreCase)
                .Select(fileGroup => fileGroup.OrderByDescending(photo => photo.FileSize).First())
                .ToList();

            var (succeeded, failed) = ExecuteTransferForPeriod(group.Period, photosForPeriod, periodTargetDirectory, transferService, copy, dryRun, verbose, metadataFile);
            totalSucceeded += succeeded;
            totalFailed += failed;

            Console.WriteLine();
        }

        Console.WriteLine($"All periods transfer complete - {totalSucceeded} files transferred successfully");
        if (totalFailed > 0)
        {
            Console.WriteLine($"Warning: {totalFailed} files failed to transfer");
            Environment.Exit(3); // Partial success
        }
        else
        {
            Environment.Exit(0); // Full success
        }
    }

    private static (int succeeded, int failed) ExecuteTransferForPeriod(TimePeriod period, List<PhotoMetadata> photos, string periodTargetDirectory, PhotoTransferService transferService, bool copy, bool dryRun, bool verbose, string metadataFile)
    {
        // Plan transfer operations
        var transferType = copy ? TransferType.Copy : TransferType.Move;
        var operations = transferService.PlanTransfer(photos, periodTargetDirectory, transferType);

        if (operations.Count == 0)
        {
            Console.WriteLine($"No files to transfer for period {period}");
            return (0, 0);
        }

        if (dryRun)
        {
            Console.WriteLine($"Dry run mode - would transfer {operations.Count} files for period {period}:");
            foreach (var operation in operations)
            {
                var actionWord = operation.Type == TransferType.Copy ? "Copy" : "Move";
                Console.WriteLine($"  Would {actionWord.ToLower()}: {operation.Photo.FilePath} -> {operation.TargetPath}");
            }
            return (operations.Count, 0);
        }

        // Execute transfer
        Console.WriteLine($"Transferring {operations.Count} files for period {period}...");
        
        if (verbose)
        {
            foreach (var operation in operations)
            {
                Console.WriteLine($"  Transferring: {operation.Photo.FileName} -> {Path.GetFileName(operation.TargetPath)}");
            }
        }

        transferService.ExecuteTransfer(operations, dryRun);

        // Check for failures
        var failed = operations.Where(op => op.Status == OperationStatus.Failed).ToList();
        var succeeded = operations.Where(op => op.Status == OperationStatus.Completed).ToList();

        if (failed.Any())
        {
            Console.Error.WriteLine($"Warning: {failed.Count} files failed to transfer for period {period}:");
            foreach (var failure in failed)
            {
                Console.Error.WriteLine($"  {failure.Photo.FileName}: {failure.ErrorMessage}");
            }
        }

        // Update metadata for successful transfers
        if (succeeded.Any())
        {
            transferService.UpdateMetadataAfterTransfer(metadataFile, succeeded);
        }

        Console.WriteLine($"Period {period} transfer complete - {succeeded.Count} files transferred successfully");
        
        return (succeeded.Count, failed.Count);
    }
}