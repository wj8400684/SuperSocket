using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using SuperSocket.Connection;

namespace SuperSocket.Client.Proxy
{
    public abstract class ProxyConnectorBase : ConnectorBase
    {
        private readonly EndPoint _proxyEndPoint;

        public IConnector DefaultConnector { get; set; }

        public ProxyConnectorBase(EndPoint proxyEndPoint)
        {
            _proxyEndPoint = proxyEndPoint;
        }

        protected abstract ValueTask<ConnectState> ConnectProxyAsync(
            EndPoint remoteEndPoint,
            IConnection connection,
            ConnectState connectState,
            CancellationToken cancellationToken);

        protected override async ValueTask<ConnectState> ConnectAsync(EndPoint remoteEndPoint, ConnectState state,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var socketConnector = DefaultConnector ?? new SocketConnector();

            ConnectState result;

            try
            {
                result = await socketConnector.ConnectAsync(_proxyEndPoint, null, cancellationToken);

                if (!result.Result)
                    return result;
            }
            catch (Exception e)
            {
                return new ConnectState
                {
                    Result = false,
                    Exception = e
                };
            }

            var connection = result.CreateConnection(new ConnectionOptions());

            try
            {
                result = await ConnectProxyAsync(remoteEndPoint, connection, result, cancellationToken);
            }
            catch (Exception e)
            {
                result.Result = false;
                result.Exception = e;
            }

            if (!result.Result)
                await connection.CloseAsync(CloseReason.ProtocolError);
            else
                await connection.DetachAsync();

            return result;
        }
    }
}