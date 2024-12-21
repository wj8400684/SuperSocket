using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using SuperSocket.Connection;
using SuperSocket.ProtoBase;

namespace SuperSocket.Client.Proxy.Request;

internal sealed class HttpRequest : ProxyRequest<TextPackageInfo>
{
    private readonly string _username;
    private readonly string _password;

    private readonly string _httpHandshake;
    private readonly IConnection _connection;
    private readonly IAsyncEnumerator<TextPackageInfo> _packStream;
    private readonly Encoding _textEncoding = Encoding.ASCII;

    private const string RequestTemplate =
        "CONNECT {0}:{1} HTTP/1.1\r\nHost: {0}:{1}\r\nProxy-Connection: Keep-Alive\r\n";

    private const string NewLine = "\r\n";

    public HttpRequest(IConnection connection,
        IAsyncEnumerator<TextPackageInfo> packStream,
        EndPoint remoteEndPoint,
        string username,
        string password)
    {
        _connection = connection;
        _packStream = packStream;
        _username = username;
        _password = password;
        _httpHandshake = remoteEndPoint switch
        {
            DnsEndPoint dnsEndPoint => string.Format(RequestTemplate, dnsEndPoint.Host, dnsEndPoint.Port),
            IPEndPoint ipEndPoint => string.Format(RequestTemplate, ipEndPoint.Address, ipEndPoint.Port),
            _ => throw new Exception($"The endpint type {remoteEndPoint.GetType()} is not supported.")
        };
    }

    public override async ValueTask<TextPackageInfo> SendHandshakeAsync()
    {
        // send request
        await _connection.SendAsync((writer) =>
        {
            writer.Write(_httpHandshake, _textEncoding);

            if (!string.IsNullOrEmpty(_username) && !string.IsNullOrEmpty(_password))
            {
                writer.Write("Proxy-Authorization: Basic ", _textEncoding);
                writer.Write(Convert.ToBase64String(_textEncoding.GetBytes($"{_username}:{_password}")), _textEncoding);
                writer.Write(NewLine, _textEncoding);
            }

            writer.Write(NewLine, _textEncoding);
        });

        return await _packStream.ReceiveAsync();
    }

    public override ValueTask<TextPackageInfo> SendAuthenticateAsync()
    {
        throw new NotImplementedException();
    }

    public override ValueTask<TextPackageInfo> SendEndPointAsync()
    {
        throw new NotImplementedException();
    }
}