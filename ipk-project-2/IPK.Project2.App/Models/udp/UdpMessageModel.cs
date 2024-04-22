using App.Enums;

namespace App.Models.udp;

public class UdpMessageModel : IBaseUdpModel, IModelWithId
{
    public UdpMessageType MessageType { get; set; } = UdpMessageType.Msg;
    public short Id { get; set; }
    public string DisplayName { get; set; }
    public string Content { get; set; }
    public UdpMessageModel()
    {
    }
}