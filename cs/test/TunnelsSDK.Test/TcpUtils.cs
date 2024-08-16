using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Microsoft.DevTunnels.Test;

internal static class TcpUtils
{
	public static int GetAvailableTcpPort(bool canReuseAddress = true)
	{
		// Get any available local tcp port
		var l = new TcpListener(IPAddress.Loopback, 0);
        if (!canReuseAddress)
        {
            l.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, false);
        }

		l.Start();
		int port = ((IPEndPoint)l.LocalEndpoint).Port;
		l.Stop();
		return port;
	}

    public static async Task<int> ReadIntToEndAsync(this Stream stream, CancellationToken cancellation)
    {
        var buffer = new byte[1024];
        var length = await stream.ReadAsync(buffer, cancellation);
        var text = Encoding.UTF8.GetString(buffer, 0, length);
        return int.Parse(text, CultureInfo.InvariantCulture);
    }
}
