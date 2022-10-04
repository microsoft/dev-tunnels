using Microsoft.DevTunnels.Connections;
using System.Diagnostics;

namespace Microsoft.DevTunnels.Test;

internal class TestTunnelRelayTunnelClient : TunnelRelayTunnelClient
{
    public TestTunnelRelayTunnelClient(TraceSource trace) : base(trace)
    {
    }

    protected override void OnSshSessionClosed(Exception exception)
    {
        base.OnSshSessionClosed(exception);
        SshSessionClosed?.Invoke(this, EventArgs.Empty);
    }

    public new event EventHandler SshSessionClosed;
}
