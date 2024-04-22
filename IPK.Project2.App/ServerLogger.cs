using System.Net;
using System.Text;
using App.Models;

namespace App;

public static class ServerLogger
{
    public static void LogInternalError(string msg)
    {
        Console.Error.WriteLine($"ERR: {msg}");
    }

    private static string BuildModelOutput(IBaseModel model)
    {
        string messageType = model switch
        {
            ErrorModel errorModel => "ERR",
            MessageModel messageModel => "MSG",
            ReplyModel replyModel => "REPLY",
            JoinModel joinModel => "JOIN",
            AuthModel authModel => "AUTH",
            ByeModel byeModel => "BYE",
            _ => "UNKNOWN"
        };

        var contentString = new StringBuilder();
        
        // Get all properties via reflection and add them to content in format key=value
        foreach (var property in model.GetType().GetProperties())
        {
            contentString.Append($"{property.Name}={property.GetValue(model)} ");
        }
        
        return $"{messageType} {contentString}";
    }

    public static void LogReceived(IBaseModel model, IPAddress address, ushort port)
    {
        Console.WriteLine($"RECV {address}:{port} | {BuildModelOutput(model)}");
    }

    public static void LogSent(IBaseModel model, IPAddress address, ushort port)
    {
        Console.WriteLine($"SENT {address}:{port} | {BuildModelOutput(model)}");
    }

    public static void LogDebug(string msg)
    {
        Console.WriteLine($"[DEBUG] {msg}");
    }
}