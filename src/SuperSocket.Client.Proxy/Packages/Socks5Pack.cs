using System.Net;

namespace SuperSocket.Client.Proxy.Packages;

internal enum Socket5ResponseType
{
    Handshake,

    AuthUserName,

    AuthEndPoint,
}

public class Socks5Address
{
    public IPAddress IpAddress { get; set; }

    public string DomainName { get; set; }
}

public class Socks5Pack
{
    public byte Version { get; set; }

    public byte Status { get; set; }

    public byte Reserve { get; set; }

    public Socks5Address DestAddr { get; set; }

    public short DestPort { get; set; }
}