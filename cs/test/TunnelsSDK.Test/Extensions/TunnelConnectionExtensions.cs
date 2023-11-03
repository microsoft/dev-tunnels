using Microsoft.DevTunnels.Connections;

namespace Microsoft.DevTunnels.Test;

public static class TunnelConnectionExtensions
{
    public async static Task WaitForConnectionStatusAsync<T>(
        this T tunnelConnection,
        ConnectionStatus status,
        Action<T> assert = null,
        CancellationToken cancellationToken = default)
        where T : TunnelConnection
    {
        while (tunnelConnection.ConnectionStatus != status)
        {
            var tcs = new TaskCompletionSource();
            void OnConnectionStatusChanged(object sender, ConnectionStatusChangedEventArgs e)
            {
                if (e.Status == status)
                {
                    assert?.Invoke(tunnelConnection);
                    tcs.TrySetResult();
                }
            }

            tunnelConnection.ConnectionStatusChanged += OnConnectionStatusChanged;
            try
            {
                if (tunnelConnection.ConnectionStatus == status)
                {
                    assert?.Invoke(tunnelConnection);
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
