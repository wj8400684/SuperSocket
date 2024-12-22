using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace SuperSocket.Kestrel.Protocol;

public class ProtocolWriter : IAsyncDisposable
{
    private readonly PipeWriter _writer;
    private readonly SemaphoreSlim _semaphore;
    private bool _disposed;

    public ProtocolWriter(Stream stream) :
        this(PipeWriter.Create(stream))
    {
    }

    public ProtocolWriter(PipeWriter writer)
        : this(writer, new SemaphoreSlim(1))
    {
    }

    public ProtocolWriter(PipeWriter writer, SemaphoreSlim semaphore)
    {
        _writer = writer;
        _semaphore = semaphore;
    }

    public async ValueTask WriteAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
    {
        var sendLockAcquired = false;

        try
        {
            await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            sendLockAcquired = true;

            if (_disposed)
            {
                return;
            }

            var result = await _writer.WriteAsync(data, cancellationToken);

            if (result.IsCanceled)
            {
                throw new OperationCanceledException();
            }

            if (result.IsCompleted)
            {
                _disposed = true;
            }
        }
        finally
        {
            if (sendLockAcquired)
                _semaphore.Release();
        }
    }

    public async ValueTask WriteAsync<TWriteMessage>(IMessageWriter<TWriteMessage> writer,
        TWriteMessage protocolMessage, CancellationToken cancellationToken = default)
    {
        var sendLockAcquired = false;

        try
        {
            await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            sendLockAcquired = true;

            if (_disposed)
            {
                return;
            }

            writer.WriteMessage(protocolMessage, _writer);

            var result = await _writer.FlushAsync(cancellationToken).ConfigureAwait(false);

            if (result.IsCanceled)
            {
                throw new OperationCanceledException();
            }

            if (result.IsCompleted)
            {
                _disposed = true;
            }
        }
        finally
        {
            if (sendLockAcquired)
                _semaphore.Release();
        }
    }

    public async ValueTask WriteManyAsync<TWriteMessage>(IMessageWriter<TWriteMessage> writer,
        IEnumerable<TWriteMessage> protocolMessages, CancellationToken cancellationToken = default)
    {
        var sendLockAcquired = false;

        try
        {
            await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            sendLockAcquired = true;

            if (_disposed)
            {
                return;
            }

            foreach (var protocolMessage in protocolMessages)
            {
                writer.WriteMessage(protocolMessage, _writer);
            }

            var result = await _writer.FlushAsync(cancellationToken).ConfigureAwait(false);

            if (result.IsCanceled)
            {
                throw new OperationCanceledException();
            }

            if (result.IsCompleted)
            {
                _disposed = true;
            }
        }
        finally
        {
            if (sendLockAcquired)
                _semaphore.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        var sendLockAcquired = false;
        await _semaphore.WaitAsync().ConfigureAwait(false);

        try
        {
            await _semaphore.WaitAsync().ConfigureAwait(false);
            sendLockAcquired = true;

            if (_disposed)
            {
                return;
            }

            _disposed = true;
        }
        finally
        {
            if (sendLockAcquired)
                _semaphore.Release();
        }
    }
}