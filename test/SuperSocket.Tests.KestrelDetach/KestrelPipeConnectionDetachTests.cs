using System;
using System.Buffers;
using System.IO;
using System.IO.Pipelines;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;
using Microsoft.Extensions.Logging.Abstractions;
using SuperSocket.Connection;
using SuperSocket.Kestrel;
using SuperSocket.ProtoBase;
using Xunit;

namespace SuperSocket.Tests.KestrelDetach;

public class KestrelPipeConnectionDetachTests
{
    [Fact]
    public async Task DetachAsync_ShouldKeepUnderlyingTransportPipesAlive()
    {
        var testCancellationToken = TestContext.Current.CancellationToken;
        var inboundPipe = new Pipe();
        var outboundPipe = new Pipe();
        var spyReader = new SpyPipeReader(inboundPipe.Reader);
        var spyWriter = new SpyPipeWriter(outboundPipe.Writer);

        await using var context = new DefaultConnectionContext
        {
            ConnectionId = "kestrel-detach-test",
            LocalEndPoint = new IPEndPoint(IPAddress.Loopback, 8742),
            RemoteEndPoint = new IPEndPoint(IPAddress.Loopback, 50000),
            Transport = new TestDuplexPipe(spyReader, spyWriter)
        };

        var connection = new KestrelPipeConnection(
            context,
            new ConnectionOptions
            {
                ReadAsDemand = false,
                Logger = NullLogger.Instance
            });

        await WriteStringAsync(inboundPipe.Writer, "CONNECT host:443 HTTP/1.1\r\n", testCancellationToken);
        TextPackageInfo firstPackage = null;
        await foreach (var package in connection.RunAsync<TextPackageInfo>(new LinePipelineFilter()))
        {
            firstPackage = package;
            break;
        }

        Assert.Equal("CONNECT host:443 HTTP/1.1", firstPackage.Text);
        await connection.DetachAsync();

        Assert.False(spyReader.CompleteAsyncCalled);
        Assert.False(spyWriter.CompleteAsyncCalled);

        const string outboundPayload = "HTTP/1.1 200 OK\r\n";
        await WriteStringAsync(context.Transport.Output, outboundPayload, testCancellationToken);
        var outboundText = await ReadExactStringAsync(
            outboundPipe.Reader,
            outboundPayload.Length,
            TimeSpan.FromSeconds(5),
            testCancellationToken);
        Assert.Equal(outboundPayload, outboundText);

        // After DetachAsync, the PipeReader must NOT have a residual cancel flag.
        // If it did, Kestrel's SslStream would abort on its first ReadAsync.
        var tryReadOk = context.Transport.Input.TryRead(out var probeResult);
        if (tryReadOk)
        {
            Assert.False(probeResult.IsCanceled,
                "PipeReader must not have residual IsCanceled after DetachAsync");
            context.Transport.Input.AdvanceTo(probeResult.Buffer.Start, probeResult.Buffer.End);
        }

        const string inboundPayload = "PING\r\n";
        await WriteStringAsync(inboundPipe.Writer, inboundPayload, testCancellationToken);
        var inboundText = await ReadExactStringAsync(
            context.Transport.Input,
            inboundPayload.Length,
            TimeSpan.FromSeconds(5),
            testCancellationToken);
        Assert.Equal(inboundPayload, inboundText);

        await inboundPipe.Writer.CompleteAsync();
        await inboundPipe.Reader.CompleteAsync();
        await outboundPipe.Writer.CompleteAsync();
        await outboundPipe.Reader.CompleteAsync();
    }

    private static async Task WriteStringAsync(PipeWriter writer, string payload, CancellationToken cancellationToken)
    {
        await writer.WriteAsync(Encoding.ASCII.GetBytes(payload), cancellationToken);
    }

    private static async Task<string> ReadExactStringAsync(
        PipeReader reader,
        int expectedBytes,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        using var timeoutCts = new CancellationTokenSource(timeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        while (true)
        {
            var result = await reader.ReadAsync(linkedCts.Token);
            var buffer = result.Buffer;

            if (buffer.Length >= expectedBytes)
            {
                var consumedBuffer = buffer.Slice(0, expectedBytes);
                var text = Encoding.ASCII.GetString(consumedBuffer.ToArray());
                reader.AdvanceTo(consumedBuffer.End, buffer.End);
                return text;
            }

            if (result.IsCompleted)
                throw new EndOfStreamException($"Expected {expectedBytes} bytes but stream completed early.");

            reader.AdvanceTo(buffer.Start, buffer.End);
        }
    }

    private sealed class TestDuplexPipe(PipeReader input, PipeWriter output) : IDuplexPipe
    {
        public PipeReader Input { get; } = input;

        public PipeWriter Output { get; } = output;
    }

    private sealed class SpyPipeReader(PipeReader innerReader) : PipeReader
    {
        private readonly PipeReader _innerReader = innerReader;

        public bool CompleteAsyncCalled { get; private set; }

        public override void AdvanceTo(SequencePosition consumed) => _innerReader.AdvanceTo(consumed);

        public override void AdvanceTo(SequencePosition consumed, SequencePosition examined) =>
            _innerReader.AdvanceTo(consumed, examined);

        public override void CancelPendingRead() => _innerReader.CancelPendingRead();

        public override void Complete(Exception exception = null) => _innerReader.Complete(exception);

        public override ValueTask CompleteAsync(Exception exception = null)
        {
            CompleteAsyncCalled = true;
            return _innerReader.CompleteAsync(exception);
        }

        public override ValueTask<ReadResult> ReadAsync(CancellationToken cancellationToken = default) =>
            _innerReader.ReadAsync(cancellationToken);

        public override bool TryRead(out ReadResult result) => _innerReader.TryRead(out result);
    }

    private sealed class SpyPipeWriter(PipeWriter innerWriter) : PipeWriter
    {
        private readonly PipeWriter _innerWriter = innerWriter;

        public bool CompleteAsyncCalled { get; private set; }

        public override bool CanGetUnflushedBytes => _innerWriter.CanGetUnflushedBytes;

        public override long UnflushedBytes => _innerWriter.UnflushedBytes;

        public override void Advance(int bytes) => _innerWriter.Advance(bytes);

        public override void CancelPendingFlush() => _innerWriter.CancelPendingFlush();

        public override void Complete(Exception exception = null) => _innerWriter.Complete(exception);

        public override ValueTask CompleteAsync(Exception exception = null)
        {
            CompleteAsyncCalled = true;
            return _innerWriter.CompleteAsync(exception);
        }

        public override ValueTask<FlushResult> FlushAsync(CancellationToken cancellationToken = default) =>
            _innerWriter.FlushAsync(cancellationToken);

        public override Memory<byte> GetMemory(int sizeHint = 0) => _innerWriter.GetMemory(sizeHint);

        public override Span<byte> GetSpan(int sizeHint = 0) => _innerWriter.GetSpan(sizeHint);
    }
}
