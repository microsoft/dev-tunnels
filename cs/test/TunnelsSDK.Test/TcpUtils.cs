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
        int totalLength = 0;
        int length;
        while ((length = await stream.ReadAsync(buffer.AsMemory(totalLength), cancellation)) > 0)
        {
            totalLength += length;
            if (buffer.AsSpan(0, totalLength).IndexOf((byte)'\n') >= 0)
            {
                break;
            }
        }

        var text = Encoding.UTF8.GetString(buffer, 0, totalLength).TrimEnd('\n');
        return int.Parse(text, CultureInfo.InvariantCulture);
    }
}
