using System.Net;
using System.Net.Sockets;
using System.Text;
using App.Enums;
using App.Models;
using App.Models.udp;
using App.Transport;
using CommandLine;
using Serilog;

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
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .WriteTo.File("log-.txt", rollingInterval: RollingInterval.Day)
            .MinimumLevel.Debug()
            .CreateLogger();

        var server = new Server(opt, Log.Logger);
        await server.Run(opt);
    }
}