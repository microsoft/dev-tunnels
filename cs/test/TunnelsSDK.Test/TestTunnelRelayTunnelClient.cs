using System.Diagnostics;

namespace Microsoft.VsSaaS.TunnelService.Test;

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
