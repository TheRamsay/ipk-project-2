using System.Net;
using System.Text;
using App.Models;
using App.Transport;

namespace App;

public static class ServerLogger
{
    public static void LogInternalError(string msg)
    {
        Console.Error.WriteLine($"ERR: {msg}");
    }

    private static string BuildModelOutput(IBaseModel model)
    {
        var messageType = model switch
        {
            ErrorModel => "ERR",
            MessageModel => "MSG",
            ReplyModel => "REPLY",
            JoinModel => "JOIN",
            AuthModel => "AUTH",
            ByeModel => "BYE",
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

    public static void LogReceived(IBaseModel model, IPEndPoint from)
    {
        Console.WriteLine($"RECV {from.Address}:{from.Port} | {BuildModelOutput(model)}");
    }
    
    public static void LogReceived(string msg, IPEndPoint from)
    {
        Console.WriteLine($"RECV {from.Address}:{from.Port} | {msg}");
    }

    public static void LogSent(IBaseModel model, IPEndPoint from)
    {
        Console.WriteLine($"SENT {from.Address}:{from.Port} | {BuildModelOutput(model)}");
    }
    
    public static void LogSent(string msg, IPEndPoint from)
    {
        Console.WriteLine($"SENT {from.Address}:{from.Port} | {msg}");
    }

    public static void LogDebug(string msg)
    {
        Console.Error.WriteLine($"[DEBUG] {msg}");
    }
}