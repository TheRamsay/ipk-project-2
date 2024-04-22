using System;
using App.Enums;
using CommandLine;

namespace App;

public class Options
{
    [Option('l', Required = true, HelpText = "Server IP or hostname")]
    public string IpAddress { get; set; }

    [Option('p', Required = false, Default = (ushort)4567, HelpText = "Server port")]
    public ushort Port { get; set; }

    [Option('d', Required = false, Default = (ushort)250, HelpText = "UDP confirmation timeout")]
    public ushort Timeout { get; set; }

    [Option('r', Default = (byte)3, Required = false, HelpText = "Maximum number of UDP retransmissions")]
    public byte RetryCount { get; set; }
}