using Microsoft.DevTunnels.Connections;

namespace Microsoft.DevTunnels.Test;

public static class TunnelConnectionExtensions
{
    public async static Task WaitForConnectionStatusAsync(
        this TunnelConnection tunnelConnection,
        ConnectionStatus status,
        CancellationToken cancellationToken = default)
    {
        while (tunnelConnection.ConnectionStatus != status)
        {
            var tcs = new TaskCompletionSource();
            void OnConnectionStatusChanged(object sender, ConnectionStatusChangedEventArgs e) =>
                tcs.TrySetResult();

            tunnelConnection.ConnectionStatusChanged += OnConnectionStatusChanged;
            try
            {
                if (tunnelConnection.ConnectionStatus == status)
                {
                    break;
                }

                await tcs.Task.WaitAsync(cancellationToken);
            }
            finally
            {
                tunnelConnection.ConnectionStatusChanged -= OnConnectionStatusChanged;
            }
        }
    }
}
