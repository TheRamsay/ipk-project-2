using App.Enums;

namespace App.Models.udp;

public class UdpConfirmModel : IBaseUdpModel
{
    public UdpMessageType MessageType { get; set; } = UdpMessageType.Confirm;
    public short RefMessageId { get; set; }

    public UdpConfirmModel()
    {

    }
}