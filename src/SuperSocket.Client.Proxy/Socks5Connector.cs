using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using SuperSocket.Client.Proxy.Filter;
using SuperSocket.Client.Proxy.Packages;
using SuperSocket.Client.Proxy.Request;
using SuperSocket.Connection;

namespace SuperSocket.Client.Proxy
{
    /// <summary>
    /// https://tools.ietf.org/html/rfc1928
    /// https://en.wikipedia.org/wiki/SOCKS
    /// </summary>
    public sealed class Socks5Connector : ProxyConnectorBase
    {
        private readonly string _username;
        private readonly string _password;

        public Socks5Connector(EndPoint proxyEndPoint)
            : base(proxyEndPoint)
        {
        }

        public Socks5Connector(EndPoint proxyEndPoint, string username, string password) :
            base(proxyEndPoint)
        {
            _username = username;
            _password = password;
        }

        protected override async ValueTask<ConnectState> ConnectProxyAsync(
            EndPoint remoteEndPoint,
            IConnection connection,
            ConnectState connectState,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            Socks5AuthPipelineFilter authFilter;

            if (string.IsNullOrWhiteSpace(_username) || string.IsNullOrWhiteSpace(_password))
            {
                authFilter = new Socks5AuthPipelineFilter(1);
            }
            else
            {
                authFilter = new Socks5AuthPipelineFilter();
            }

            var packStream = connection.GetPackageStream(authFilter);

            var request = new Socks5Request(connection, packStream, remoteEndPoint, _username, _password);

            var response = await request.SendHandshakeAsync();

            if (!HandleResponse(response, Socket5ResponseType.Handshake, out var errorMessage))
            {
                connectState.Result = false;
                connectState.Exception = new Exception(errorMessage);
                return connectState;
            }

            if (response.Status == 0x02) // need pass auth
            {
                response = await request.SendAuthenticateAsync();

                if (!HandleResponse(response, Socket5ResponseType.AuthUserName, out errorMessage))
                {
                    connectState.Result = false;
                    connectState.Exception = new Exception(errorMessage);
                    return connectState;
                }
            }

            response = await request.SendEndPointAsync();

            if (!HandleResponse(response, Socket5ResponseType.AuthEndPoint, out errorMessage))
            {
                connectState.Result = false;
                connectState.Exception = new Exception(errorMessage);
            }
            else
            {
                connectState.Result = true;
            }

            return connectState;
        }

        private static bool HandleResponse(Socks5Pack response, Socket5ResponseType responseType, out string errorMessage)
        {
            errorMessage = null;

            switch (responseType)
            {
                case Socket5ResponseType.Handshake:
                {
                    if (response.Status != 0x00 && response.Status != 0x02)
                    {
                        errorMessage = $"failed to connect to proxy , protocol violation";
                        return false;
                    }

                    break;
                }
                case Socket5ResponseType.AuthUserName:
                {
                    if (response.Status != 0x00)
                    {
                        errorMessage = "failed to connect to proxy ,  username/password combination rejected";
                        return false;
                    }

                    break;
                }
                default:
                {
                    if (response.Status != 0x00)
                    {
                        errorMessage = response.Status switch
                        {
                            (0x02) => "connection not allowed by ruleset",
                            (0x03) => "network unreachable",
                            (0x04) => "host unreachable",
                            (0x05) => "connection refused by destination host",
                            (0x06) => "TTL expired",
                            (0x07) => "command not supported / protocol error",
                            (0x08) => "address type not supported",
                            _ => "general failure",
                        };
                        errorMessage = $"failed to connect to proxy ,  {errorMessage}";
                        return false;
                    }

                    break;
                }
            }

            return true;
        }
    }
}