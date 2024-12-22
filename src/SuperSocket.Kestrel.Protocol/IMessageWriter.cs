using System.Buffers;

namespace SuperSocket.Kestrel.Protocol;

public interface IMessageWriter<in TMessage>
{
    void WriteMessage(TMessage message, IBufferWriter<byte> output);
}