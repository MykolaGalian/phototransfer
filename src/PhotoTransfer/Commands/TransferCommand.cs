using System.CommandLine;
using System.Text.RegularExpressions;
using PhotoTransfer.Models;
using PhotoTransfer.Services;

namespace PhotoTransfer.Commands;

public static class TransferCommand
{
    public static Command Create()
    {
        // Create a command that accepts date patterns like --2012-01 or ranges like 2020-01..2022-12
        var dateArgument = new Argument<string?>(
            "period",
            "Date period in format YYYY-MM or range YYYY-MM..YYYY-MM (optional when using --all)")
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
            "Show detailed transfer information with real-time progress (speed, ETA, files skipped)");

        var allOption = new Option<bool>(
            "--all",
            "Transfer all photos organized by their monthly periods");

        var command = new Command("--transfer", "Transfer photos from specified period, period range, or all periods with optimized parallel copying")
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
                await ExecuteTransferAllPeriods(index, transferService, copy, dryRun, target, verbose, metadataFile);
            }
            else
            {
                await ExecuteTransferSinglePeriod(period!, index, transferService, copy, dryRun, target, verbose, metadataFile);
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

    private static async Task ExecuteTransferSinglePeriod(string period, PhotoIndex index, PhotoTransferService transferService, bool copy, bool dryRun, string? target, bool verbose, string metadataFile)
    {
        // Check if it's a range (e.g., "2020-01..2022-12")
        var range = TimePeriod.ParseRange(period);

        if (range.HasValue)
        {
            // Execute transfer for range of periods
            await ExecuteTransferForRange(range.Value.start, range.Value.end, index, transferService, copy, dryRun, target, verbose, metadataFile);
            return;
        }

        // Parse the date period as single period
        TimePeriod timePeriod;
        try
        {
            timePeriod = TimePeriod.Parse(period);
        }
        catch (FormatException)
        {
            Console.Error.WriteLine($"Error: Invalid date format: {period}. Expected format: YYYY-MM or YYYY-MM..YYYY-MM");
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
        await ExecuteTransferForPeriod(timePeriod, photosForPeriod, periodTargetDirectory, transferService, copy, dryRun, verbose, metadataFile);
    }

    private static async Task ExecuteTransferForRange(TimePeriod startPeriod, TimePeriod endPeriod, PhotoIndex index, PhotoTransferService transferService, bool copy, bool dryRun, string? target, bool verbose, string metadataFile)
    {
        // Get all periods in range
        var periods = TimePeriod.GetRange(startPeriod, endPeriod);

        Console.WriteLine($"Processing range: {startPeriod} to {endPeriod} ({periods.Count} periods)");

        // Group photos by period within the range
        var periodGroups = index.Photos
            .Where(photo =>
            {
                var photoPeriod = new TimePeriod(photo.EffectiveDate.Year, photo.EffectiveDate.Month);
                return photoPeriod.IsInRange(startPeriod, endPeriod);
            })
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
            Console.Error.WriteLine($"Error: No photos found in range: {startPeriod} to {endPeriod}");
            Environment.Exit(2);
            return;
        }

        Console.WriteLine($"Found photos in {periodGroups.Count} periods within range:");
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

            // Filter duplicates for this period
            var photosForPeriod = group.Photos
                .GroupBy(photo => photo.FileName, StringComparer.OrdinalIgnoreCase)
                .Select(fileGroup => fileGroup.OrderByDescending(photo => photo.FileSize).First())
                .ToList();

            var (succeeded, failed) = await ExecuteTransferForPeriod(group.Period, photosForPeriod, periodTargetDirectory, transferService, copy, dryRun, verbose, metadataFile);
            totalSucceeded += succeeded;
            totalFailed += failed;

            Console.WriteLine();
        }

        Console.WriteLine($"Range transfer complete - {totalSucceeded} files transferred successfully");
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

    private static async Task ExecuteTransferAllPeriods(PhotoIndex index, PhotoTransferService transferService, bool copy, bool dryRun, string? target, bool verbose, string metadataFile)
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

            var (succeeded, failed) = await ExecuteTransferForPeriod(group.Period, photosForPeriod, periodTargetDirectory, transferService, copy, dryRun, verbose, metadataFile);
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

    private static async Task<(int succeeded, int failed)> ExecuteTransferForPeriod(TimePeriod period, List<PhotoMetadata> photos, string periodTargetDirectory, PhotoTransferService transferService, bool copy, bool dryRun, bool verbose, string metadataFile)
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

        // Execute transfer with optimized async method
        Console.WriteLine($"Transferring {operations.Count} files for period {period}...");

        // Create progress reporter
        var progressReporter = new Progress<TransferProgress>(progress =>
        {
            var percent = progress.PercentComplete;
            var speed = progress.FormattedSpeed;
            var transferred = progress.FormattedTotalTransferred;
            var eta = progress.EstimatedTimeRemaining;

            // Clear current line and print progress
            Console.Write($"\r  Progress: {progress.CompletedOperations}/{progress.TotalOperations} ({percent:F1}%) | {speed} | {transferred} transferred");

            if (progress.SkippedOperations > 0)
            {
                Console.Write($" | {progress.SkippedOperations} skipped");
            }

            if (eta.TotalSeconds > 0)
            {
                Console.Write($" | ETA: {eta:hh\\:mm\\:ss}");
            }
        });

        // Use optimized async transfer with progress reporting
        await transferService.ExecuteTransferAsync(
            operations,
            dryRun,
            verifyIntegrity: true,  // Enable hash verification
            skipExisting: true,      // Skip files that already exist with same hash
            maxDegreeOfParallelism: 4,  // Copy 4 files in parallel
            progress: verbose ? progressReporter : null
        );

        // Move to next line after progress
        if (verbose)
        {
            Console.WriteLine();
        }

        // Check for failures
        var failed = operations.Where(op => op.Status == OperationStatus.Failed).ToList();
        var succeeded = operations.Where(op => op.Status == OperationStatus.Completed).ToList();
        var skipped = operations.Where(op => op.ErrorMessage == "Skipped (already exists with same hash)").ToList();

        if (failed.Any())
        {
            Console.Error.WriteLine($"Warning: {failed.Count} files failed to transfer for period {period}:");
            foreach (var failure in failed)
            {
                Console.Error.WriteLine($"  {failure.Photo.FileName}: {failure.ErrorMessage}");
            }
        }

        // Update metadata for successful transfers (batch operation - single write!)
        var actuallyTransferred = succeeded.Where(s => s.ErrorMessage != "Skipped (already exists with same hash)").ToList();
        if (actuallyTransferred.Any())
        {
            if (verbose)
            {
                Console.WriteLine($"Updating metadata for {actuallyTransferred.Count} transferred files...");
            }
            transferService.UpdateMetadataAfterTransferBatch(metadataFile, succeeded);
        }

        var message = $"Period {period} transfer complete - {actuallyTransferred.Count} files transferred";
        if (skipped.Any())
        {
            message += $", {skipped.Count} skipped (already exist)";
        }
        Console.WriteLine(message);

        return (succeeded.Count, failed.Count);
    }
}