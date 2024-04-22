using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Net.Sockets;
using System.Text;
using App.Enums;
using App.Exceptions;
using App.Models;
using App.Models.udp;

namespace App.Transport;

public class UdpTransport : ITransport
{
    private readonly CancellationToken _cancellationToken;
    private readonly UdpClient _client;
    private readonly Options _options;
    // If we have exceeded the retry count, we need to signal the main thread to throw an exception
    private readonly SemaphoreSlim _retryExceededSignal = new(0, 1);
    // We need to keep track of messages that we have already processed, so we don't process them again, only confirm them
    private readonly HashSet<short> _processedMessages = new();
    private readonly Queue<byte[]> _messages = new();

    private PendingMessage? _pendingMessage;
    private short _messageIdSequence;
    private ProtocolStateBox? _protocolState;
    private IPAddress _ipAddress;

    public event EventHandler<IBaseModel>? OnMessageReceived;
    public event EventHandler? OnConnected;
    public event EventHandler<IModelWithId> OnTimeoutExpired;
    public event EventHandler? OnMessageDelivered;

    private event EventHandler<UdpConfirmModel>? OnMessageConfirmed;

    public UdpTransport(Options options, CancellationToken cancellationToken, UdpClient client, IEnumerable<byte[]> messages)
    {
        _cancellationToken = cancellationToken;
        _options = options;
        _client = client;
        _messages = new Queue<byte[]>(messages);

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
        _client.Client.Bind(new IPEndPoint(IPAddress.Any, 4568));
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
        throw new ServerUnreachableException("Max retries reached, message not delivered");
    }

    public async Task Start(ProtocolStateBox protocolState)
    {
        _protocolState = protocolState;
        OnConnected?.Invoke(this, EventArgs.Empty);

        Console.WriteLine("STarting");
        // Wait until receiving loop is finished or retry count is exceeded
        await await Task.WhenAny(Receive(), RetryExceededHandler());
    }

    private async Task Receive()
    {
        var ipv4 = await GetIpAddress(_options.IpAddress);
        //
        _ipAddress = ipv4 ?? throw new ServerUnreachableException("Invalid server address");

        // UDP client is listening on all ports
        // _client.Client.Bind(new IPEndPoint(_ipAddress, 0));
        // _client.Client.Bind(new IPEndPoint(IPAddress.Any, 0));

        while (!_cancellationToken.IsCancellationRequested)
        {
            var response = await GetNextMessage();
            var from = response.RemoteEndPoint;
            var dataBuffer = response.Buffer;
            var parsedData = ParseMessage(dataBuffer);
            
            if (parsedData is UdpConfirmModel confirmModel)
            {
                OnMessageConfirmed?.Invoke(this, confirmModel);
                continue;
            }

            try
            {
                ModelValidator.Validate(parsedData.ToBaseModel());
            }
            catch (ValidationException e)
            {
                throw new InvalidMessageReceivedException($"Unable to validate received message: {e.Message}");
            }


            switch (parsedData)
            {
                // If we have already processed this message, just confirm it and continue
                case IModelWithId modelWithId when _processedMessages.Contains(modelWithId.Id):
                    await Send(new UdpConfirmModel { RefMessageId = modelWithId.Id });
                    continue;
                // If we haven't processed this message yet, confirm it and process it
                case IModelWithId modelWithId:
                    {
                        
                        var model = parsedData.ToBaseModel();

                        // After authentication, we need to reconnect to a different port, for private communication
                        if (model is ReplyModel && _protocolState.State is ProtocolState.Auth)
                        {
                            _options.Port = (ushort)from.Port;
                        }

                        Console.WriteLine("Sending confirmation");
                        await Send(new UdpConfirmModel { RefMessageId = modelWithId.Id });
                        Console.WriteLine("Confirmation sent");
                        _processedMessages.Add(modelWithId.Id);
                        OnMessageReceived?.Invoke(this, model);
                        break;
                    }
            }
        }
    }

    private async Task Send(IBaseUdpModel data)
    {
        var buffer = IBaseUdpModel.Serialize(data);
        var sendTo = new IPEndPoint(_ipAddress, _options.Port);
        await _client.SendAsync(buffer, sendTo, _cancellationToken);

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

    private IBaseUdpModel ParseMessage(byte[] data)
    {
        return IBaseUdpModel.Deserialize(data);
    }

    private void OnMessageConfirmedHandler(object? sender, UdpConfirmModel data)
    {
        // If confirmation is for a message we haven't sent, ignore it
        if (_pendingMessage?.Model.Id != data.RefMessageId)
        {
            return;
        }

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
            _pendingMessage.Retries++;
            await Send((IBaseUdpModel)data);
        }
        else
        {
            // Big problem ⚠️(server is eepy I guess 😴, we throw an exception to the main thread to handle it)
            _retryExceededSignal.Release();
        }
    }

    private async Task<IPAddress?> GetIpAddress(string hostname)
    {
        return (await Dns.GetHostAddressesAsync(hostname, _cancellationToken))
            .FirstOrDefault(ip => ip.AddressFamily == AddressFamily.InterNetwork);
    }

    private async Task<UdpReceiveResult> GetNextMessage()
    {
        if (_messages.Count != 0)
        {
            Console.WriteLine("Getting message from the queue");
            return new UdpReceiveResult(_messages.Dequeue(), new IPEndPoint(IPAddress.Any, 0));
        }

        Console.WriteLine("Waiting for message");
        return await _client.ReceiveAsync(_cancellationToken);
    }

}