using App.Enums;

namespace App.Models.udp;

public class UdpByeModel : IBaseUdpModel, IModelWithId
{
    public UdpMessageType MessageType { get; set; } = UdpMessageType.Bye;
    public short Id { get; set; }

    public UdpByeModel()
    {
    }
}