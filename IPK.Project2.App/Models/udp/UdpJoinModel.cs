using System.ComponentModel.DataAnnotations;
using App.Enums;

namespace App.Models.udp;

public class UdpJoinModel : IBaseUdpModel, IModelWithId
{
    public UdpMessageType MessageType { get; set; } = UdpMessageType.Join;
    public short Id { get; set; }

    [RegularExpression("[A-z0-9-]{1, 20}", ErrorMessage = "ChannelId has to be alphanumerical with length from 1 to 20 characters")]
    public required string ChannelId { get; set; }

    [RegularExpression("[0x21-7E]{1, 20}", ErrorMessage = "DisplayName has to have printable characters with length from 1 to 128 characters")]
    public required string DisplayName { get; set; }
    public UdpJoinModel()
    {
    }
}