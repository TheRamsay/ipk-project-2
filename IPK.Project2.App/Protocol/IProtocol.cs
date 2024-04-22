using App.Models;

namespace App;

public interface IProtocol
{
    public event EventHandler<IBaseModel>? OnMessage;
    public event EventHandler? OnConnected;
    
    Task Start();
    Task Disconnect();
    Task Send(IBaseModel model);
}