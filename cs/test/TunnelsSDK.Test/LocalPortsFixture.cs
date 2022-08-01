using System.Net;
using System.Net.Sockets;

namespace Microsoft.VsSaaS.TunnelService.Test
{
    public class LocalPortsFixture : IDisposable
    {
        private readonly TcpListener listener;
        private readonly TcpListener listener1;

        public LocalPortsFixture()
        {
            // Get the local tcp ports
            this.listener = new TcpListener(IPAddress.Loopback, port: 0);
            this.listener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, false);
            this.listener.Start();

            this.listener1 = new TcpListener(IPAddress.Loopback, port: 0);
            this.listener1.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, false);
            this.listener1.Start();
        }

        public ushort Port => (ushort)(((IPEndPoint)this.listener.LocalEndpoint).Port);
        public ushort Port1 => (ushort)(((IPEndPoint)this.listener1.LocalEndpoint).Port);

        public void Dispose()
        {
            this.listener.Stop();
            this.listener1.Stop();
        }
    }
}
