// <copyright file="RetryTcpListenerFactory.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.
// </copyright>

using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DevTunnels.Ssh.Tcp;

namespace Microsoft.VsSaaS.TunnelService
{
    /// <summary>
    /// Implementation of a TCP listener factory that retries forwarding with nearby ports and falls back to a random port.
    /// We make the assumption that the remote port that is being connected to and localPort numbers are the same.
    /// </summary>
    internal class RetryTcpListenerFactory : ITcpListenerFactory
    {
        /// <inheritdoc />
        public Task<TcpListener> CreateTcpListenerAsync(
            IPAddress localIPAddress,
            int localPort,
            bool canChangePort,
            TraceSource trace,
            CancellationToken cancellation)
        {
            if (localIPAddress == null) throw new ArgumentNullException(nameof(localIPAddress));

            const ushort maxOffet = 10;
            TcpListener listener;

            for (ushort offset = 0; ; offset++)
            {
                // After reaching the max offset, pass 0 to pick a random available port.
                var localPortNumber = offset == maxOffet ? 0 : localPort + offset;
                try
                {
                    listener = new TcpListener(localIPAddress, localPortNumber);
                    listener.Server.SetSocketOption(
                        SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, false);

                    listener.Start();

                    // It is assumed that the localPort passed in is the same as the host port
                    trace.TraceInformation($"Forwarding from {listener.LocalEndpoint} to host port {localPort}.");
                    return Task.FromResult(listener);
                }
                catch (SocketException sockex)
                when ((sockex.SocketErrorCode == SocketError.AccessDenied ||
                    sockex.SocketErrorCode == SocketError.AddressAlreadyInUse) &&
                    offset < maxOffet && canChangePort)
                {
                    trace.TraceEvent(TraceEventType.Verbose, 1, "Listening on port " + localPortNumber + " failed: " + sockex.Message);
                    trace.TraceEvent(TraceEventType.Verbose, 2, "Incrementing port and trying again");
                    continue;
                }
            }
        }
    }
}
