using System.Net;
using System.Net.Sockets;
using App.Enums;
using App.Exceptions;
using App.Models;
using App.Transport;
using Serilog;

namespace App;

public class Ipk24ChatProtocol: IProtocol
{
    public readonly ITransport Transport;
    // _messageDeliveredSignal is used for waiting for the message to be delivered (confirmed by the server)
    private readonly SemaphoreSlim _messageDeliveredSignal = new(0, 1);
    // _messageProcessedSignal is used for waiting for the message to be processed (for example, receiving a reply to the AUTH message)
    private readonly SemaphoreSlim _messageProcessedSignal = new(0, 1);
    // _endSignal is used for throwing exceptions from EventHandlers
    private readonly SemaphoreSlim _endSignal = new(0, 1);
    // Used for cancelling the message receive loop
    private readonly CancellationTokenSource _cancellationTokenSource;
    private readonly ILogger _logger;
    private readonly Options _options;

    private readonly IList<Client> _clients;
    private Client _client;

    private Exception? _exceptionToThrow;
    private ProtocolStateBox? _protocolState;

    public event EventHandler<IBaseModel>? OnMessage;
    public event EventHandler? OnConnected;

    public Ipk24ChatProtocol(ITransport transport, CancellationTokenSource cancellationTokenSource,  IList<Client> clients, Client client, ILogger logger, Options options)
    {
        Transport = transport;
        _cancellationTokenSource = cancellationTokenSource;
        _clients = clients;
        _client = client;
        _logger = logger;
        _options = options;
        
        // Event subscription
        Transport.OnMessageReceived += OnMessageReceivedHandler;
        Transport.OnMessageDelivered += OnMessageDeliveredHandler;
        Transport.OnConnected += OnConnectedHandler;
    }


    public async Task Start()
    {
        _protocolState = new ProtocolStateBox(ProtocolState.Accept);

        try
        {
            // Start the receive loop
            // This will end if any of the following happens:
            // - Server sends a malformed message
            // - Server sends a BYE message
            // - Server closes the connection
            // - Server sends an error message
            // - Server sends a message that is not expected in the current state
            // The ProtocolEnHandler function is used for throwing exceptions from EventHandlers
            // This is necessary because the exceptions thrown in EventHandlers are not caught by the try-catch block
            await await Task.WhenAny(Transport.Start(_protocolState), ProtocolEndHandler());
            ServerLogger.LogDebug("Finished transporting");
            // await _transport.Start(_protocolState);
            // Console.WriteLine("Finished!!!!");
        }
        // If server sends a malformed message, send ERR, BYE and disconnect
        catch (InvalidMessageReceivedException e)
        {
            var errorModel = new ErrorModel
            {
                Content = e.Message,
                DisplayName = "Server"
            };

            ServerLogger.LogDebug("Sending error message");
            await SendInternal(errorModel);
            ServerLogger.LogDebug("Disconnecting");
            await Disconnect();
            ServerLogger.LogDebug("Disconnected");
            // Exception is rethrown for proper ending of the protocol in the ChatClient
            // throw;
        }
        catch (Exception e) when (e is ClientUnreachableException or SocketException)
        {
            ServerLogger.LogInternalError(e.Message);
        }
        catch (OperationCanceledException)
        {
            ServerLogger.LogDebug("Operation cancelled");
            await Disconnect();
        }
        catch (Exception e)
        {
            ServerLogger.LogDebug(e.Message);
            await Disconnect();
        }
    }

    public async Task Send(IBaseModel model)
    {
        switch (model)
        {
            case ReplyModel data:
                await Reply(data);
                break;
            case AuthModel data:
                await Auth(data);
                break;
            case JoinModel data:
                await Join(data);
                break;
            case MessageModel data:
                await Message(data);
                break;
        }
    }

    private async Task Auth(AuthModel data)
    {
        await SendInternal(data, true);
    }

    private async Task Join(JoinModel data)
    {
        await SendInternal(data, true);
    }

    private async Task Message(MessageModel data)
    {
        await SendInternal(data);
    }
    
    private async Task Reply(ReplyModel data)
    {
        await SendInternal(data);
    }

    public async Task Disconnect()
    {
        await SendInternal(new ByeModel());
        ServerLogger.LogDebug("Sent bye");
        await Task.Delay(_options.Timeout);
        await _cancellationTokenSource.CancelAsync();
        Transport.Disconnect();
        ServerLogger.LogDebug("Final disconnect");
    }

    private async Task WaitForDelivered(Task task)
    {
        ServerLogger.LogDebug("WaitForDelivered is up");
        var tasks = new[] { _messageDeliveredSignal.WaitAsync(_cancellationTokenSource.Token), task };
        await Task.WhenAll(tasks);
        ServerLogger.LogDebug("WaitForDelivered is done");
    }

    private async Task WaitForDeliveredAndProcessed(Task task)
    {
        var tasks = new[] { _messageDeliveredSignal.WaitAsync(_cancellationTokenSource.Token), _messageProcessedSignal.WaitAsync(_cancellationTokenSource.Token), task };
        await Task.WhenAll(tasks);
    }

    private async Task ProtocolEndHandler()
    {
        await _endSignal.WaitAsync(_cancellationTokenSource.Token);

        if (_exceptionToThrow is not null)
        {
            throw _exceptionToThrow;
        }
    }

    private async Task SendInternal(IBaseModel data, bool waitForProcessed = false)
    {
        var task = data switch
        {
            ReplyModel replyModel => Transport.Reply(replyModel),
            AuthModel authModel => Transport.Auth(authModel),
            JoinModel joinModel => Transport.Join(joinModel),
            MessageModel messageModel => Transport.Message(messageModel),
            ByeModel => Transport.Bye(),
            ErrorModel errorModel => Transport.Error(errorModel),
            _ => throw new InternalException($"Invalid message type {data}")
        };
        
        ServerLogger.LogSent(data, _client.Address);
        // If message needs to be processed, wait for the message to be delivered and processed
        // This is for example needed when sending an AUTH message, because we need to know if the server accepted it
        if (waitForProcessed)
        {
            await WaitForDeliveredAndProcessed(task);
        }
        // If the message does not need to be processed, wait only for the message to be delivered
        // In case of TCP, this will go through immediately, because the message is sent immediately
        // And the underlying transport layer will handle it for us
        // But in case of UDP, we need to wait for the message to be delivered (this is verified by receiving the CONFIRM message)
        else
        {
            await WaitForDelivered(task);
        }
    }

    private async void Receive(IBaseModel model)
    {
        if (_protocolState is null)
        {
            throw new InternalException("Protocol not started");
        }

        switch (_protocolState.State, model)
        {
            case (ProtocolState.Accept or ProtocolState.Auth, AuthModel data):
                var authenticated = AuthUser(data);
                if (authenticated)
                {
                    await Transport.StartPrivateConnection();
                    await Reply(new ReplyModel { Status = true, Content = "Welcome to the server"});
                    await AnnounceChannelChange(data.DisplayName, _client.Channel);
                    _protocolState.SetState(ProtocolState.Open);
                }
                else
                {
                    await Reply(new ReplyModel { Status = false, Content = "Invalid auth attempt"});
                    _protocolState.SetState(ProtocolState.Auth);
                }
                break;
            case (ProtocolState.Open, MessageModel data):
                _client.DisplayName = data.DisplayName;
                await Broadcast(data, _client.Channel);
                break;
            case (ProtocolState.Open, JoinModel data):
                var joined = JoinUser(data, out var previousChannel);
                if (joined)
                {
                    _client.DisplayName = data.DisplayName;
                    await Reply(new ReplyModel { Status = true, Content = "Welcome to the channel"});
                    await AnnounceChannelChange(data.DisplayName, previousChannel, false);
                    await AnnounceChannelChange(data.DisplayName, _client.Channel);
                }
                else
                {
                    await Reply(new ReplyModel { Status = false, Content = "Invalid join attempt"});
                }

                break;
            case (ProtocolState.Open, ErrorModel data):
                _exceptionToThrow = new ClientException(data);
                _protocolState.SetState(ProtocolState.End);
                break;
            case (_, ByeModel):
                _protocolState.SetState(ProtocolState.End);
                break;
            default:
                _exceptionToThrow = new InvalidMessageReceivedException($"No action for {model} in state {_protocolState.State}");
                _protocolState.SetState(ProtocolState.End);
                break;
        }

        if (_protocolState.State == ProtocolState.End)
        {
            await AnnounceChannelChange(_client!.DisplayName, _client.Channel, false);
            _endSignal.Release();
        }
    }
    
    private bool AuthUser(AuthModel data)
    {
        _client.DisplayName = data.DisplayName;
        _client.Username = data.Username;
        return true;
    }
    
    private bool JoinUser(JoinModel data, out string previousChannel)
    {
        previousChannel = _client.Channel;
        _client.DisplayName = data.DisplayName;
        _client.Channel = data.ChannelId;
        return true;
    }
    
    private async Task AnnounceChannelChange(string displayName, string channel, bool joined = true)
    {
        await Broadcast(new MessageModel
        {
            DisplayName = "Server",
            Content = $"{displayName} has {(joined ? "joined" : "left")} {channel}"
        }, channel, joined);
    }
    
    private async Task Broadcast(MessageModel data, string channel, bool sendSelf = false)
    {
        foreach (var client in _clients)
        {
            if ((!sendSelf && client.Protocol == _client.Protocol) || client.Channel != channel)
            {
                continue;
            }

            await client.Protocol.Message(data);
        }
    }
    
    #region Event handlers

    private void OnMessageDeliveredHandler(object? sender, EventArgs args)
    {
        try
        {
            ServerLogger.LogDebug("Message delivered");
            _messageDeliveredSignal.Release();
        }
        catch (Exception e)
        {
            _exceptionToThrow = e;
            _endSignal.Release();
        }
    }

    private void OnMessageReceivedHandler(object? sender, IBaseModel model)
    {
        try
        {
            ServerLogger.LogReceived(model, _client.Address);
            Receive(model);
        }
        catch (Exception e)
        {
            _exceptionToThrow = e;
            _endSignal.Release();
        }
    }

    private void OnConnectedHandler(object? sender, IPEndPoint address)
    {
        try
        {
            // Console.WriteLine("Client connected yupeeee");
            _client.Address = address;
            // Console.WriteLine($"Client address is {_client.Address}");
            // OnConnected?.Invoke(sender, args);
        }
        catch (Exception e)
        {
            _exceptionToThrow = e;
            _endSignal.Release();
        }
    }

    #endregion
}