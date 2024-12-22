using System;
using System.Buffers;

namespace SuperSocket.Kestrel.Protocol;

public interface IMessageReader<TMessage>
{
    bool TryParseMessage(in ReadOnlySequence<byte> input, ref SequencePosition consumed, ref SequencePosition examined,
        out TMessage message);
}