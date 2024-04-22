using App.Models;

namespace App;

public interface IProtocol
{
    public event EventHandler<IBaseModel>? OnMessage;
    public event EventHandler? OnConnected;

    Task Start();
    Task Disconnect();
    void Rename(string displayName);
    Task Send(IBaseModel model);
}