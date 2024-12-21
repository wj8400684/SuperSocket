using System.Threading.Tasks;

namespace SuperSocket.Client.Proxy.Request;

internal abstract class ProxyRequest<TPacket>
{
    public abstract ValueTask<TPacket> SendHandshakeAsync();

    public abstract ValueTask<TPacket> SendAuthenticateAsync();

    public abstract ValueTask<TPacket> SendEndPointAsync();
}