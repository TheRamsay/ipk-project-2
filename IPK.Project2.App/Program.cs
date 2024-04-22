using System.Net;
using System.Net.Sockets;
using System.Text;
using App.Enums;
using App.Models;
using App.Models.udp;
using App.Transport;
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
        // TcpListener server = new TcpListener(IPAddress.Parse(opt.IpAddress), opt.Port);
        UdpClient server = new UdpClient(4567);
        Console.WriteLine("MKEJMKKWfew");
        
        List<Client> clients = new();
        
        var cancellationTokenSource = new CancellationTokenSource();
        
        // server.Start();

        var i = 0;

        while (true)
        {
            // var socket = await server.AcceptTcpClientAsync();
            var data = await server.ReceiveAsync();
            Console.WriteLine("RECeived first message");
            // Console.WriteLine("new client accepted");
            // var client = new Client
            // {
            //     Protocol = new Ipk24ChatProtocol(new TcpTransport(opt, CancellationToken.None, socket), cancellationTokenSource, clients),
            //     Username = $"User{clients.Count}", 
            // };
            
            var protocol = new Ipk24ChatProtocol(
                new UdpTransport(
                    opt, 
                    CancellationToken.None, 
                    server,
                    new List<byte[]> { data.Buffer }), 
                cancellationTokenSource, 
                clients
            );
            
            protocol.OnMessage += async (sender, model) =>
            {
                Console.WriteLine($"JOOOO ZPRAVA {model}");
            };
            //
            // clients.Add(client);
            protocol.Start().ContinueWith(_ => clients.RemoveAll(x => x.Protocol == protocol));
        }
        
        // server.Stop();
        server.Close();
    }

    public static async Task RunClient(Client client)
    {
        try
        {
            await client.Protocol.Start();
        } catch (Exception e)
        {
            await client.Protocol.Disconnect();
            Console.WriteLine("MLEM ERRROR");
        }
    }

}