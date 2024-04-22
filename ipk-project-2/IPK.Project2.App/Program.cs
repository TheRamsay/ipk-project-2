
using CommandLine;

namespace App;

static class Program
{
    static async Task<int> Main(string[] args)
    {
        new Parser(with => with.CaseInsensitiveEnumValues = true)
            .ParseArguments<Options>(args)
            .WithParsed(o => RunClient(o).Wait()); 
        
        
        if (args.Any(arg => arg is "-h" or "--help"))
        {
            PrintHelp();
            return 0;
        }

        var statusCode = 0;

        new Parser(with => with.CaseInsensitiveEnumValues = true)
            .ParseArguments<Options>(args)
            .WithParsed(o => RunClient(o).Wait())
            .WithNotParsed(errors =>
            {
                foreach (var error in errors)
                {
                    if (error is HelpRequestedError or VersionRequestedError)
                    {
                        PrintHelp();
                    }
                    else
                    {
                        ServerLogger.LogInternalError(error.ToString() ?? string.Empty);
                    }
                }

                statusCode = 1;
            });

        return statusCode;
    }
    public static async Task RunClient(Options opt)
    {
        try
        {
            var server = new Server(opt);
            await server.Run(opt);
        }
        catch (Exception e)
        {
            ServerLogger.LogInternalError(e.Message);
            Environment.Exit(1);
        }
    }
    
    public static void PrintHelp()
    {
        Console.WriteLine("Usage: program_name [options]\n");
        Console.WriteLine("Options:");
        Console.WriteLine("  -l <IP_address|hostname>\tServer IP or hostname");
        Console.WriteLine("  -p <port_number>\tServer port (default: 4567)");
        Console.WriteLine("  -d <timeout_value>\tUDP confirmation timeout (default: 250)");
        Console.WriteLine("  -r <num_retransmissions>\tMaximum number of UDP retransmissions (default: 3)");
        Console.WriteLine("  -h, --help\tPrints program help output and exits");
    }
}