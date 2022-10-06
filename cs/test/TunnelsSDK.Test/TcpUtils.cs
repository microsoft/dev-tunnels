using System.Net;
using System.Net.Sockets;

namespace Microsoft.DevTunnels.Test;

internal static class TcpUtils
{
	public static int GetAvailableTcpPort()
	{
		// Get any available local tcp port
		var l = new TcpListener(IPAddress.Loopback, 0);
		l.Start();
		int port = ((IPEndPoint)l.LocalEndpoint).Port;
		l.Stop();
		return port;
	}
}
