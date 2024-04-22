using System.Net;
using System.Net.Sockets;
using App.Transport;
using Serilog;

namespace App;

public class Server
{
    private readonly Options _opt;
    private readonly List<Client> _clients = new();
    private readonly ILogger _logger;

    public Server(Options opt, ILogger logger)
    {
        _opt = opt;
        _logger = logger;
    }

    public async Task Run(Options options)
    {
        await Task.WhenAll(RunTcp(), RunUdp());
    }

    private async Task RunTcp()
    { 
        var server = new TcpListener(IPAddress.Parse(_opt.IpAddress), _opt.Port);
        var cancellationTokenSource = new CancellationTokenSource();
        
        server.Start();
        
        while (!cancellationTokenSource.Token.IsCancellationRequested)
        {
            var socket = await server.AcceptTcpClientAsync(cancellationTokenSource.Token);

            var client = new Client();
            _clients.Add(client);
            
            var protocol = new Ipk24ChatProtocol(new TcpTransport(_opt, CancellationToken.None, socket),
                cancellationTokenSource, _clients, client, _logger);
            
            client.Protocol = protocol;


            protocol.Start().ContinueWith(_ =>
            {
                ServerLogger.LogDebug("Removing client from client list");
                return _clients.RemoveAll(c => c == client);
            });
        }
        
        server.Stop();
    }
    
    private async Task RunUdp()
    {
        var server = new UdpClient(4567);
        var cancellationTokenSource = new CancellationTokenSource();
        
        while (!cancellationTokenSource.Token.IsCancellationRequested)
        {
            var data = await server.ReceiveAsync();

            var client = new Client();
            _clients.Add(client);
            
            var protocol = new Ipk24ChatProtocol(
                new UdpTransport(
                    _opt, 
                    CancellationToken.None, 
                    server,
                    new List<UdpReceiveResult> { data }), 
                cancellationTokenSource, 
                _clients,
                client,
                _logger
            );
            
            client.Protocol = protocol;
            
            protocol.Start().ContinueWith(_ => _clients.RemoveAll(x => x.Protocol == protocol));
        }
        
        server.Close();
    }
}

