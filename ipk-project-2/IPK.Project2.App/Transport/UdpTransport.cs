using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Net.Sockets;
using App.Enums;
using App.Exceptions;
using App.Models;
using App.Models.udp;

namespace App.Transport;

public class UdpTransport : ITransport
{
    private readonly CancellationToken _cancellationToken;
    private UdpClient _client;
    private readonly Options _options;
    // If we have exceeded the retry count, we need to signal the main thread to throw an exception
    private readonly SemaphoreSlim _retryExceededSignal = new(0, 1);
    // We need to keep track of messages that we have already processed, so we don't process them again, only confirm them
    private readonly HashSet<short> _processedMessages = new();
    private readonly Queue<UdpReceiveResult> _messages;
    
    private IPEndPoint? _from;
    private PendingMessage? _pendingMessage;
    private short _messageIdSequence;
    private ProtocolStateBox? _protocolState;

    public event EventHandler<IBaseModel>? OnMessageReceived;
    public event EventHandler<IPEndPoint>? OnConnected;
    public event EventHandler<IModelWithId> OnTimeoutExpired;
    public event EventHandler? OnMessageDelivered;

    private event EventHandler<UdpConfirmModel>? OnMessageConfirmed;

    public UdpTransport(Options options, CancellationToken cancellationToken, UdpClient client, IEnumerable<UdpReceiveResult> messages)
    {
        _cancellationToken = cancellationToken;
        _options = options;
        _client = client;
        _messages = new Queue<UdpReceiveResult>(messages);

        // Event subscription
        OnMessageConfirmed += OnMessageConfirmedHandler;
        OnTimeoutExpired += OnTimeoutExpiredHandler;
    }

    public void Disconnect()
    {
        _client.Close();
    }
    
    public async Task StartPrivateConnection()
    {
        var ipAddress = await Server.GetIpAddress(_options.IpAddress);
        
        if (ipAddress is null)
        {
            throw new ClientUnreachableException("Invalid server address");
        }
        
        var endpoint = new IPEndPoint(ipAddress, 0);
        _client = new UdpClient(endpoint);
    }

    public async Task Auth(AuthModel data)
    {
        await Send(data.ToUdpModel(_messageIdSequence++));
    }

    public async Task Join(JoinModel data)
    {
        await Send(data.ToUdpModel(_messageIdSequence++));
    }
    
    public async Task Reply(ReplyModel data)
    {
        await Send(data.ToUdpModel(_messageIdSequence++));
    }

    public async Task Message(MessageModel data)
    {
        await Send(data.ToUdpModel(_messageIdSequence++));
    }

    public async Task Error(ErrorModel data)
    {
        await Send(data.ToUdpModel(_messageIdSequence++));
    }
    public async Task Bye()
    {
        await Send(new ByeModel().ToUdpModel(_messageIdSequence++));
    }

    private async Task RetryExceededHandler()
    {
        await _retryExceededSignal.WaitAsync(_cancellationToken);
        ServerLogger.LogDebug("Max retries reached, message not delivered");
        throw new ClientUnreachableException("Max retries reached, message not delivered");
    }

    public async Task Start(ProtocolStateBox protocolState)
    {
        _protocolState = protocolState;

        // Console.WriteLine("STarting");
        // Wait until receiving loop is finished or retry count is exceeded
        await await Task.WhenAny(Receive(), RetryExceededHandler());
    }

    private async Task Receive()
    {
        // var ipv4 = await GetIpAddress(_options.IpAddress);
        //
        // _ipAddress = ipv4 ?? throw new ServerUnreachableException("Invalid server address");

        // UDP client is listening on all ports
        // _client.Client.Bind(new IPEndPoint(_ipAddress, 0));
        // _client.Client.Bind(new IPEndPoint(IPAddress.Any, 0));

        while (!_cancellationToken.IsCancellationRequested)
        {
            var response = await GetNextMessage();
            await HandleReceive(response);
        }
    }

    private async Task HandleReceive(UdpReceiveResult response)
    {
        var from = response.RemoteEndPoint;
        
        // We are connected after first message is received
        if (_from is null)
        {
            OnConnected?.Invoke(this, from);
        }
        
        _from = from;
        var dataBuffer = response.Buffer;
        var parsedData = ParseMessage(dataBuffer);
        
        if (parsedData is UdpConfirmModel confirmModel)
        {
            OnMessageConfirmed?.Invoke(this, confirmModel);
            return;
        }

        try
        {
            ModelValidator.Validate(parsedData.ToBaseModel());
        }
        catch (ValidationException e)
        {
            throw new InvalidMessageReceivedException(e.Message);
        }

        switch (parsedData)
        {
            // If we have already processed this message, just confirm it and continue
            case IModelWithId modelWithId when _processedMessages.Contains(modelWithId.Id):
                ServerLogger.LogSent("CONFIRM", _from);
                await Send(new UdpConfirmModel { RefMessageId = modelWithId.Id });
                return;
            // If we haven't processed this message yet, confirm it and process it
            case IModelWithId modelWithId:
                {
                    
                    var model = parsedData.ToBaseModel();

                    // After authentication, we need to reconnect to a different port, for private communication
                    // Console.WriteLine("Sending confirmation");
                    await Send(new UdpConfirmModel { RefMessageId = modelWithId.Id });
                    // Console.WriteLine("Confirmation sent");
                    _processedMessages.Add(modelWithId.Id);
                    OnMessageReceived?.Invoke(this, model);
                    break;
                }
        }
    }

    private async Task Send(IBaseUdpModel data)
    {
        var buffer = IBaseUdpModel.Serialize(data);
        await _client.SendAsync(buffer, _from, _cancellationToken);

        // If the message is a model with ID, we need to handle proper confirmation from the server
        if (data is IModelWithId modelWithId)
        {
            // If it is first time sending this message, create a new pending message
            _pendingMessage ??= new PendingMessage { Model = modelWithId, Retries = 0 };

            // Background task for handling message timeout
            // Rest of this method is non-blocking, so we can continue with other messages
            // Confirmation and retry handling is done in the background, by EventHandlers
            Task.Run(async () =>
            {
                // Task is eepy 😴
                await Task.Delay(_options.Timeout, _cancellationToken);
                OnTimeoutExpired.Invoke(this, modelWithId);
            }, _cancellationToken);
        }
    }

    public async Task Redirect(UdpReceiveResult result)
    {
        await HandleReceive(result);
    }

    private IBaseUdpModel ParseMessage(byte[] data)
    {
        return IBaseUdpModel.Deserialize(data);
    }

    private void OnMessageConfirmedHandler(object? sender, UdpConfirmModel data)
    {
        // If confirmation is for a message we haven't sent, ignore it
        ServerLogger.LogReceived("CONFIRM", _from ?? new IPEndPoint(IPAddress.Any, 0));
        if (_pendingMessage?.Model.Id != data.RefMessageId)
        {
            return;
        }

        ServerLogger.LogDebug($"Message with ID {data.RefMessageId} confirmed successfully");
        OnMessageDelivered?.Invoke(this, EventArgs.Empty);
        _pendingMessage = null;
    }

    private async void OnTimeoutExpiredHandler(object? sender, IModelWithId data)
    {
        if (_pendingMessage?.Model.Id != data.Id)
        {
            return;
        }

        // If we haven't exceeded the retry count, retry the message
        if (_pendingMessage?.Retries < _options.RetryCount)
        {
            ServerLogger.LogDebug($"Resending message with ID {data.Id}");
            _pendingMessage.Retries++;
            await Send((IBaseUdpModel)data);
        }
        else
        {
            // Big problem ⚠️(server is eepy I guess 😴, we throw an exception to the main thread to handle it)
            _retryExceededSignal.Release();
        }
    }

    private async Task<UdpReceiveResult> GetNextMessage()
    {
        if (_messages.Count != 0)
        {
            return _messages.Dequeue();
        }

        return await _client.ReceiveAsync(_cancellationToken);
    }
}