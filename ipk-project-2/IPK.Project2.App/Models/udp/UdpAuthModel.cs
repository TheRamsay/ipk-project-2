using System.Text;
using App.Enums;

namespace App.Models.udp;

public class UdpAuthModel : IModelWithId, IBaseUdpModel
{
    public UdpMessageType MessageType { get; set; } = UdpMessageType.Auth;
    public short Id { get; set; }

    public string Username { get; set; }

    public string DisplayName { get; set; }

    public string Secret { get; set; }
    public UdpAuthModel()
    {

    }
}