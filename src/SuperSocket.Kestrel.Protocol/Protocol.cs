using System.Buffers;
using System.IO.Pipelines;
using System.Threading;
using Microsoft.AspNetCore.Connections;

namespace SuperSocket.Kestrel.Protocol;

public static class Protocol
{
    public static ProtocolWriter CreateWriter(this ConnectionContext connection) => new(connection.Transport.Output);

    public static ProtocolWriter CreateWriter(this ConnectionContext connection, SemaphoreSlim semaphore) =>
        new(connection.Transport.Output, semaphore);

    public static ProtocolReader CreateReader(this ConnectionContext connection) => new(connection.Transport.Input);

    public static PipeReader CreatePipeReader(this ConnectionContext connection, IMessageReader<ReadOnlySequence<byte>> messageReader)
        => new MessagePipeReader(connection.Transport.Input, messageReader);
}
