using App.Enums;

namespace App.Models.udp;

public class UdpReplyModel : IBaseUdpModel, IModelWithId
{
    public UdpMessageType MessageType { get; set; } = UdpMessageType.Reply;
    public short Id { get; set; }
    public bool Status { get; set; }
    public short RefMessageId { get; set; }
    public string Content { get; set; }
    public UdpReplyModel()
    {

    }
}