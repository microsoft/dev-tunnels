using Microsoft.DevTunnels.Ssh;
using Microsoft.DevTunnels.Ssh.Events;

namespace Microsoft.DevTunnels.Test;

public static class SshExtensions
{
    public static async Task WaitUntiClosedAsync(
        this MultiChannelStream stream,
        CancellationToken cancellationToken = default)
    {
        if (stream.IsClosed)
        {
            return;
        }

        var closedTcs = new TaskCompletionSource();
        void OnClosed(object sender, SshSessionClosedEventArgs e) => closedTcs.TrySetResult();
        stream.Closed += OnClosed;
        try
        {
            if (stream.IsClosed)
            {
                return;
            }

            await closedTcs.Task.WaitAsync(cancellationToken);
        }
        finally
        {
            stream.Closed -= OnClosed;
        }
    }
}
