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

namespace Microsoft.DevTunnels.Connections;

/// <summary>
/// Implementation of a TCP listener factory that retries forwarding with nearby ports and falls back to a random port.
/// We make the assumption that the remote port that is being connected to and localPort numbers are the same.
/// </summary>
internal class RetryTcpListenerFactory : ITcpListenerFactory
{
    public RetryTcpListenerFactory(IPAddress localAddress)
    {
        LocalAddress = localAddress;

        if (localAddress == IPAddress.Loopback)
        {
            LocalAddressV6 = IPAddress.IPv6Loopback;
        }
        else if (localAddress == IPAddress.Any)
        {
            LocalAddressV6 = IPAddress.IPv6Any;
        }
    }

    public IPAddress LocalAddress { get; }

    public IPAddress? LocalAddressV6 { get; }

    /// <inheritdoc />
    public Task<TcpListener> CreateTcpListenerAsync(
        int? remotePort,
        IPAddress localIPAddress,
        int localPort,
        bool canChangeLocalPort,
        TraceSource trace,
        CancellationToken cancellation)
    {
        const ushort maxOffet = 10;
        TcpListener listener;

        // The SSH protocol may specify a local IP address for forwarding, but that is ignored
        // by tunnels. Instead, the tunnel client can specify the local IP address.
        if (localIPAddress.AddressFamily == AddressFamily.InterNetwork &&
            localIPAddress != LocalAddress)
        {
            trace.TraceInformation(
                $"Using local interface address {LocalAddress} instead of {localIPAddress}.");
            localIPAddress = LocalAddress;
        }
        else if (localIPAddress.AddressFamily == AddressFamily.InterNetworkV6 &&
            LocalAddressV6 != null && localIPAddress != LocalAddressV6)
        {
            trace.TraceInformation(
                $"Using local interface address {LocalAddressV6} instead of {localIPAddress}.");
            localIPAddress = LocalAddressV6;
        }

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

                if (remotePort != null)
                {
                    trace.TraceInformation($"Forwarding from {listener.LocalEndpoint} to host port {remotePort}.");
                }

                return Task.FromResult(listener);
            }
            catch (SocketException sockex)
            when ((sockex.SocketErrorCode == SocketError.AccessDenied ||
                sockex.SocketErrorCode == SocketError.AddressAlreadyInUse) &&
                offset < maxOffet && canChangeLocalPort)
            {
                trace.TraceEvent(TraceEventType.Verbose, 1, "Listening on port " + localPortNumber + " failed: " + sockex.Message);
                trace.TraceEvent(TraceEventType.Verbose, 2, "Incrementing port and trying again");
                continue;
            }
        }
    }
}
