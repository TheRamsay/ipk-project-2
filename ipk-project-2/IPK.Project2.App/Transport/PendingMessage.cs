using App.Models.udp;

namespace App.Transport;

public class PendingMessage
{
    public required IModelWithId Model { get; set; }
    public short Retries { get; set; }
}