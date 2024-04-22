using System.Net;
using System.Net.Sockets;
using App.Models.udp;
using App.Transport;
using Serilog;

namespace App;

public class Server(Options opt, ILogger logger)
{
    private readonly List<Client> _clients = new();
    private readonly CancellationTokenSource _cancellationTokenSource = new();

    public async Task Run(Options options)
    {
        await Task.WhenAll(RunTcp(), RunUdp());
    }

    private async Task RunTcp()
    { 
        var server = new TcpListener(IPAddress.Parse(opt.IpAddress), opt.Port);
        
        server.Start();
        
        while (!_cancellationTokenSource.Token.IsCancellationRequested)
        {
            var socket = await server.AcceptTcpClientAsync(_cancellationTokenSource.Token);

            var client = new Client();
            _clients.Add(client);
            
            var protocol = new Ipk24ChatProtocol(new TcpTransport(opt, CancellationToken.None, socket),
                _cancellationTokenSource, _clients, client, logger, opt);
            
            client.Protocol = protocol;

            ServerLogger.LogDebug("Creating new client protocol");
            
            protocol.Start().ContinueWith(_ =>
            {
                ServerLogger.LogDebug("Removing client from client list");
                _clients.RemoveAll(c => c == client);
                ServerLogger.LogDebug($"Number of clients: {_clients.Count}");
            });
        }
        
        server.Stop();
    }
    
    private async Task RunUdp()
    {
        var endpoint = new IPEndPoint(IPAddress.Parse(opt.IpAddress), opt.Port);
        var server = new UdpClient(endpoint);
        var cancellationTokenSource = new CancellationTokenSource();
        
        while (!cancellationTokenSource.Token.IsCancellationRequested)
        {
            var data = await server.ReceiveAsync();

            var client = _clients.FirstOrDefault(x =>
                Equals(x.Address!.Address, data.RemoteEndPoint.Address) && x.Address.Port == data.RemoteEndPoint.Port);

            if (client is not null)
            {
                ServerLogger.LogDebug("Redirecting data to existing client");
                await ((UdpTransport)client.Protocol.Transport).Redirect(data);
                continue;
            }

            client = new Client();
            _clients.Add(client);
            
            var protocol = new Ipk24ChatProtocol(
                new UdpTransport(
                    opt, 
                    CancellationToken.None, 
                    server,
                    new List<UdpReceiveResult> { data }), 
                cancellationTokenSource, 
                _clients,
                client,
                logger,
                opt
            );
            
            client.Protocol = protocol;
            ServerLogger.LogDebug("Creating new client protocol");
            
            protocol.Start().ContinueWith(_ =>
            {
                ServerLogger.LogDebug("Removing client from client list");
                _clients.RemoveAll(c => c == client);
                ServerLogger.LogDebug($"Number of clients: {_clients.Count}");
            });
        }
        
        server.Close();
    }
}