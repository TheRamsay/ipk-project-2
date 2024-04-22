﻿using System.Net.Sockets;
using System.Text;
using App;
using App.Enums;
using App.Models;
using App.Transport;

public class Client
{
    public required Ipk24ChatProtocol Protocol { get; set; }
    public string Username { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Channel { get; set; } = "general";
}