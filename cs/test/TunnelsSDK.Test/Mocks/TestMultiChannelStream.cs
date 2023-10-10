using System.Diagnostics;
using Microsoft.DevTunnels.Ssh;

namespace Microsoft.DevTunnels.Test;
public class TestMultiChannelStream : MultiChannelStream
{
    public TestMultiChannelStream(Stream transportStream, TraceSource trace = null)
        : base(transportStream, trace)
    {
    }

    public async Task CloseAsync(SshDisconnectReason reason)
    {
        await Session.CloseAsync(reason);
        Session.Dispose();
        await TransportStream.DisposeAsync();
    }
}
