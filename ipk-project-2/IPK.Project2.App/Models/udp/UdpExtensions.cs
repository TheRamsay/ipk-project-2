using App.Exceptions;

namespace App.Models.udp;

public static class UdpExtensions
{
    public static IBaseModel ToBaseModel(this IBaseUdpModel udpModel)
    {
        return udpModel switch
        {
            UdpAuthModel data => new AuthModel
            {
                DisplayName = data.DisplayName,
                Secret = data.Secret,
                Username = data.Username
            },
            UdpJoinModel data => new JoinModel { ChannelId = data.ChannelId, DisplayName = data.DisplayName },
            UdpMessageModel data => new MessageModel { Content = data.Content, DisplayName = data.DisplayName },
            UdpErrorModel data => new ErrorModel { Content = data.Content, DisplayName = data.DisplayName },
            UdpReplyModel data => new ReplyModel { Content = data.Content, Status = data.Status },
            UdpByeModel _ => new ByeModel(),
            _ => throw new InvalidMessageReceivedException($"Unknown UDP message type {udpModel}")
        };
    }

    public static IBaseUdpModel ToUdpModel(this IBaseModel baseModel, short messageId)
    {
        return baseModel switch
        {
            AuthModel data => new UdpAuthModel
            {
                DisplayName = data.DisplayName,
                Secret = data.Secret,
                Username = data.Username,
                Id = messageId
            },
            JoinModel data => new UdpJoinModel { ChannelId = data.ChannelId, DisplayName = data.DisplayName, Id = messageId },
            MessageModel data => new UdpMessageModel { Content = data.Content, DisplayName = data.DisplayName, Id = messageId },
            ErrorModel data => new UdpErrorModel { Content = data.Content, DisplayName = data.DisplayName, Id = messageId },
            ReplyModel data => new UdpReplyModel { Content = data.Content, Status = data.Status, Id = messageId },
            ByeModel _ => new UdpByeModel { Id = messageId },
            _ => throw new InvalidMessageReceivedException("Unknown base model type")
        };
    }
}