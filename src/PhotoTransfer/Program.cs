using System.CommandLine;
using PhotoTransfer.Commands;

namespace PhotoTransfer;

/// <summary>
/// PhotoTransfer Console Application Entry Point
/// </summary>
public class Program
{
    public static async Task<int> Main(string[] args)
    {
        // Handle special date pattern commands like --2012-01
        if (args.Any(TransferCommand.IsDatePattern))
        {
            return await TransferCommand.HandleDatePattern(args);
        }

        var rootCommand = new RootCommand("PhotoTransfer - Photo organization tool")
        {
            IndexCommand.Create(),
            TransferCommand.Create(),
            StatCommand.Create(),
            TypesCommand.Create()
        };

        return await rootCommand.InvokeAsync(args);
    }
}