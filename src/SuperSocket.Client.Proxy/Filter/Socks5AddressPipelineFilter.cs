using System;
using System.Buffers;
using System.Net;
using System.Text;
using SuperSocket.Client.Proxy.Packages;
using SuperSocket.ProtoBase;

namespace SuperSocket.Client.Proxy.Filter;

internal sealed class Socks5AddressPipelineFilter : FixedHeaderPipelineFilter<Socks5Pack>
{
    public Socks5AddressPipelineFilter()
        : base(5)
    {
    }

    protected override Socks5Pack DecodePackage(ref ReadOnlySequence<byte> buffer)
    {
        var reader = new SequenceReader<byte>(buffer);
        reader.TryRead(out var version);
        reader.TryRead(out var status);
        reader.TryRead(out var reserve);
        reader.TryRead(out var addressType);

        var address = new Socks5Address();

        switch (addressType)
        {
            case 0x01:
            {
                const int addrLen = 4;
                address.IpAddress = new IPAddress(reader.Sequence.Slice(reader.Consumed, addrLen).ToArray());
                reader.Advance(addrLen);
                break;
            }
            case 0x04:
            {
                const int addrLen = 16;
                address.IpAddress = new IPAddress(reader.Sequence.Slice(reader.Consumed, addrLen).ToArray());
                reader.Advance(addrLen);
                break;
            }
            case 0x03:
            {
                reader.TryRead(out byte addrLen);
                var seq = reader.Sequence.Slice(reader.Consumed, addrLen);
                address.DomainName = seq.GetString(Encoding.ASCII);
                reader.Advance(addrLen);
                break;
            }
            default:
                throw new ProtocolException($"Unsupported addressType: {addressType}");
        }

        reader.TryReadBigEndian(out ushort port);

        return new Socks5Pack
        {
            Version = version,
            Status = status,
            Reserve = reserve,
            DestAddr = address,
            DestPort = (short)port
        };
    }

    protected override int GetBodyLengthFromHeader(ref ReadOnlySequence<byte> buffer)
    {
        var reader = new SequenceReader<byte>(buffer);
        reader.Advance(3);
        reader.TryRead(out var addressType);

        switch (addressType)
        {
            case 0x01:
                return 6 - 1;
            case 0x04:
                return 18 - 1;
            case 0x03:
                reader.TryRead(out byte domainLen);
                return domainLen + 2;
            default:
                throw new Exception($"Unsupported addressType: {addressType}");
        }
    }
}