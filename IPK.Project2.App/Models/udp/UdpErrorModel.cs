using App.Enums;

namespace App.Models.udp;

public class UdpErrorModel : IBaseUdpModel, IModelWithId
{
    public UdpMessageType MessageType { get; set; } = UdpMessageType.Err;
    public short Id { get; set; }
    public string DisplayName { get; set; }
    public string Content { get; set; }
    public UdpErrorModel()
    {
    }
}