
using CommandLine;

namespace App;

static class Program
{
    static async Task Main(string[] args)
    {
        new Parser(with => with.CaseInsensitiveEnumValues = true)
            .ParseArguments<Options>(args)
            .WithParsed(o => RunClient(o).Wait()); 
    }
    public static async Task RunClient(Options opt)
    {
        var server = new Server(opt);
        await server.Run(opt);
    }
}