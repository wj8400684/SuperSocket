using System.Buffers;
using SuperSocket.Client.Proxy.Packages;
using SuperSocket.ProtoBase;

namespace SuperSocket.Client.Proxy.Filter;

internal sealed class Socks5AuthPipelineFilter : FixedSizePipelineFilter<Socks5Pack>
{
    private readonly int _authStep;

    public Socks5AuthPipelineFilter(int authStep = 0)
        : base(2)
    {
        _authStep = authStep;
    }

    protected override Socks5Pack DecodePackage(ref ReadOnlySequence<byte> buffer)
    {
        var reader = new SequenceReader<byte>(buffer);
        reader.TryRead(out var version);
        reader.TryRead(out var status);

        if (_authStep == 0)
            NextFilter = new Socks5AuthPipelineFilter(1);
        else
            NextFilter = new Socks5AddressPipelineFilter();

        return new Socks5Pack
        {
            Version = version,
            Status = status
        };
    }
}
