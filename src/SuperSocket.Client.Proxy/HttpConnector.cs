using System;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SuperSocket.Client.Proxy.Request;
using SuperSocket.Connection;
using SuperSocket.ProtoBase;

namespace SuperSocket.Client.Proxy
{
    public sealed class HttpConnector : ProxyConnectorBase
    {
        private readonly string _username;
        private readonly string _password;

        private const string ResponsePrefix = "HTTP/1.";
        private const char Space = ' ';

        public HttpConnector(EndPoint proxyEndPoint)
            : base(proxyEndPoint)
        {
        }

        public HttpConnector(EndPoint proxyEndPoint, string username, string password) : base(
            proxyEndPoint)
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

            var packStream = connection.GetPackageStream(new LinePipelineFilter(Encoding.ASCII));

            var request = new HttpRequest(connection, packStream, remoteEndPoint, _username, _password);

            var p = await request.SendHandshakeAsync();

            if (!HandleResponse(p, out var errorMessage))
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

        private static bool HandleResponse(TextPackageInfo p, out string message)
        {
            message = string.Empty;

            if (p == null)
                return false;

            var pos = p.Text.IndexOf(Space);

            // validating response
            if (!p.Text.StartsWith(ResponsePrefix, StringComparison.OrdinalIgnoreCase) || pos <= 0)
            {
                message = "Invalid response";
                return false;
            }

            if (!int.TryParse(p.Text.AsSpan().Slice(pos + 1, 3), out var statusCode))
            {
                message = "Invalid response";
                return false;
            }

            if (statusCode < 200 || statusCode > 299)
            {
                message = $"Invalid status code {statusCode}";
                return false;
            }

            return true;
        }
    }
}