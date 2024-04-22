using App.Models;

namespace App;

public interface IProtocol
{
    Task Start();
    Task Disconnect();
}