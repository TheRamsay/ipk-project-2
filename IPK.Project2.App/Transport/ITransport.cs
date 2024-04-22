using App.Enums;
using App.Models;

namespace App.Transport;

public interface ITransport
{
    public event EventHandler<IBaseModel> OnMessageReceived;
    public event EventHandler OnMessageDelivered;
    public event EventHandler OnConnected;

    public Task StartPrivateConnection();
    public Task Auth(AuthModel data);
    public Task Join(JoinModel data);
    public Task Reply(ReplyModel data);
    public Task Message(MessageModel data);
    public Task Error(ErrorModel data);
    public Task Bye();
    public Task Start(ProtocolStateBox protocolState);
    public void Disconnect();
}