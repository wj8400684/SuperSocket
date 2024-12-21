using System;
using System.Buffers;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using SuperSocket.Client.Proxy.Packages;
using SuperSocket.Connection;

namespace SuperSocket.Client.Proxy.Request;

internal sealed class Socks5Request : ProxyRequest<Socks5Pack>
{
    private static readonly byte[] EndPointHeader = { 0x05, 0x01, 0x00 };
    private static readonly byte[] Handshake = { 0x05, 0x01, 0x00 };
    private static readonly byte[] AuthHandshake = { 0x05, 0x02, 0x00, 0x02 };

    private readonly string _username;
    private readonly string _password;
    private readonly EndPoint _remoteEndPoint;
    private readonly IConnection _connection;
    private readonly byte[] _handshakeRequest;
    private readonly IAsyncEnumerator<Socks5Pack> _packStream;

    public Socks5Request(IConnection connection,
        IAsyncEnumerator<Socks5Pack> packStream,
        EndPoint remoteEndPoint,
        string username,
        string password)
    {
        _connection = connection;
        _packStream = packStream;
        _username = username;
        _password = password;
        _remoteEndPoint = remoteEndPoint;
        _handshakeRequest = string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password)
            ? Handshake
            : AuthHandshake;
    }

    public override async ValueTask<Socks5Pack> SendHandshakeAsync()
    {
        await _connection.SendAsync(_handshakeRequest);
        return await _packStream.ReceiveAsync();
    }

    public override async ValueTask<Socks5Pack> SendAuthenticateAsync()
    {
        await _connection.SendAsync(writer =>
        {
            const byte v1 = 0x01;

            writer.WriteLittleEndian(v1);
            writer.WriterStringWithLength(_username, Encoding.ASCII);
            writer.WriterStringWithLength(_password, Encoding.ASCII);
        });

        return await _packStream.ReceiveAsync();
    }

    public override async ValueTask<Socks5Pack> SendEndPointAsync()
    {
        const byte constInterDns = 0x03;
        const byte constInterNetwork = 0x01;
        const byte constInterNetworkV6 = 0x04;

        await _connection.SendAsync(write =>
        {
            int port;
            write.Write(EndPointHeader);

            switch (_remoteEndPoint)
            {
                case IPEndPoint ipEndPoint:
                    port = ipEndPoint.Port;
                    write.WriteLittleEndian(ipEndPoint.AddressFamily switch
                    {
                        AddressFamily.InterNetwork => constInterNetwork,
                        AddressFamily.InterNetworkV6 => constInterNetworkV6,
                        _ => throw new NotSupportedException(nameof(ipEndPoint.AddressFamily))
                    });
                    write.Write(ipEndPoint.Address.GetAddressBytes());
                    break;
                case DnsEndPoint dnsEndPoint:
                {
                    port = dnsEndPoint.Port;
                    write.WriteLittleEndian(constInterDns);
                    write.WriterStringWithLength(dnsEndPoint.Host, Encoding.ASCII);
                    break;
                }
                default:
                    throw new NotSupportedException(nameof(_remoteEndPoint));
            }

            write.WriteBigEndian((ushort)port);
        });

        return await _packStream.ReceiveAsync();
    }
}