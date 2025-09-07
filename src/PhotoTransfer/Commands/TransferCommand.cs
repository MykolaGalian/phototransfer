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
        var dateArgument = new Argument<string>(
            "period",
            "Date period in format YYYY-MM");

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

        var command = new Command("--transfer", "Transfer photos from specified period")
        {
            dateArgument,
            copyOption,
            dryRunOption,
            targetOption,
            verboseOption
        };

        command.SetHandler(async (period, copy, dryRun, target, verbose) =>
        {
            await ExecuteTransferCommand(period, copy, dryRun, target, verbose);
        }, dateArgument, copyOption, dryRunOption, targetOption, verboseOption);

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

    private static async Task ExecuteTransferCommand(string period, bool copy, bool dryRun, string? target, bool verbose)
    {
        try
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

            // Find photos for the specified period
            var transferService = new PhotoTransferService(metadataStore);
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

            // Plan transfer operations
            var transferType = copy ? TransferType.Copy : TransferType.Move;
            var operations = transferService.PlanTransfer(photosForPeriod, periodTargetDirectory, transferType);

            if (dryRun)
            {
                Console.WriteLine("Dry run mode - showing what would be transferred:");
                foreach (var operation in operations)
                {
                    var actionWord = operation.Type == TransferType.Copy ? "Copy" : "Move";
                    Console.WriteLine($"Would {actionWord.ToLower()}: {operation.Photo.FilePath} -> {operation.TargetPath}");
                }
                Console.WriteLine($"Total: {operations.Count} files would be transferred");
                Environment.Exit(0);
                return;
            }

            // Execute transfer
            Console.WriteLine($"Starting transfer of {operations.Count} files...");
            
            if (verbose)
            {
                foreach (var operation in operations)
                {
                    Console.WriteLine($"Transferring: {operation.Photo.FileName} -> {Path.GetFileName(operation.TargetPath)}");
                }
            }

            transferService.ExecuteTransfer(operations, dryRun);

            // Check for failures
            var failed = operations.Where(op => op.Status == OperationStatus.Failed).ToList();
            var succeeded = operations.Where(op => op.Status == OperationStatus.Completed).ToList();

            if (failed.Any())
            {
                Console.Error.WriteLine($"Warning: {failed.Count} files failed to transfer:");
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

            Console.WriteLine($"Transfer complete - {succeeded.Count} files transferred successfully");
            if (failed.Any())
            {
                Environment.Exit(3); // Partial success
            }
            else
            {
                Environment.Exit(0); // Full success
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
        await ExecuteTransferCommand(period, copy, dryRun, target, verbose);
        return 0;
    }
}