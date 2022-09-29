// <copyright file="TunnelHostBase.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.
// </copyright>

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DevTunnels.Ssh;
using Microsoft.DevTunnels.Ssh.Algorithms;
using Microsoft.DevTunnels.Ssh.Messages;
using Microsoft.DevTunnels.Ssh.Tcp;
using Microsoft.DevTunnels.TunnelService.Contracts;

namespace Microsoft.DevTunnels.TunnelService;

/// <summary>
/// Base class for Hosts that host one tunnel
/// </summary>
public abstract class TunnelHost : TunnelConnection, ITunnelHost
{
    internal const string RefreshPortsRequestType = "RefreshPorts";

    /// <summary>
    /// Sessions created between this host and clients. Lock on this hash set to be thread-safe.
    /// </summary>
    protected HashSet<SshServerSession> sshSessions = new();

    /// <summary>
    /// Creates a new instance of the <see cref="TunnelHost" /> class.
    /// </summary>
    public TunnelHost(ITunnelManagementClient managementClient, TraceSource trace) : base(managementClient, trace)
    {
    }

    /// <summary>
    /// Enumeration of sessions created between this host and clients. Thread safe.
    /// </summary>
    protected IEnumerable<SshServerSession> SshSessions 
    { 
        get 
        {
            lock (this.sshSessions)
            {
                return this.sshSessions.ToArray();
            }
        }
    }

    /// <summary>
    /// Port Forwarders between host and clients
    /// </summary>
    public ConcurrentDictionary<SessionPortKey, RemotePortForwarder> RemoteForwarders { get; } = new ConcurrentDictionary<SessionPortKey, RemotePortForwarder>();

    /// <summary>
    /// Private key used for connections.
    /// </summary>
    protected IKeyPair HostPrivateKey { get; } = SshAlgorithms.PublicKey.ECDsaSha2Nistp384.GenerateKeyPair();

    /// <inheritdoc />
    protected override string TunnelAccessScope => TunnelAccessScopes.Host;

    /// <inheritdoc />
    public async Task StartAsync(Tunnel tunnel, CancellationToken cancellation)
    {
        Requires.NotNull(tunnel, nameof(tunnel));

        if (Tunnel != null)
        {
            throw new InvalidOperationException(
                "Already hosting a tunnel. Use separate instances to host multiple tunnels.");
        }

        Requires.NotNull(tunnel.Ports!, nameof(tunnel.Ports));
        await ConnectTunnelSessionAsync(tunnel, cancellation);
    }

    internal async Task ForwardPortAsync(
        PortForwardingService pfs,
        TunnelPort port,
        CancellationToken cancellation)
    {
        var sessionId = Requires.NotNull(pfs.Session.SessionId!, nameof(pfs.Session.SessionId));

        var portNumber = (int)port.PortNumber;

        if (pfs.LocalForwardedPorts.Any((p) => p.LocalPort == portNumber))
        {
            // The port is already forwarded. This may happen if we try to add the same port twice after reconnection.
            return;
        }

        // When forwarding from a Remote port we assume that the RemotePortNumber
        // and requested LocalPortNumber are the same.
        RemotePortForwarder? forwarder;

        try
        {
            forwarder = await pfs.ForwardFromRemotePortAsync(
                IPAddress.Loopback,
                portNumber,
                IPAddress.Loopback.ToString(),
                portNumber,
                cancellation);
        }
        catch (SshConnectionException)
        {
            // Ignore exception caused by the session being closed; it will be reported elsewhere.
            // Treat it as equivalent to the client rejecting the forwarding request.
            forwarder = null;
        }

        if (forwarder == null)
        {
            // The forwarding request was rejected by the client.
            return;
        }

        // Capture the remote forwarder for the session id / remote port pair.
        // This is needed later to stop forwarding for this port when the remote forwarder is disposed.
        // Note when the client tries to open an SSH channel to PFS, the port forwarding service checks
        // its remoteConnectors whether the port is being forwarded.
        // Disposing of the RemotePortForwarder stops the forwarding and removes the remote connector
        // from PFS.remoteConnectors.
        RemoteForwarders.TryAdd(
            new SessionPortKey(sessionId, (ushort)forwarder.LocalPort),
            forwarder);
        return;
    }


    /// <inheritdoc />
    public async Task RefreshPortsAsync(CancellationToken cancellation)
    {
        if (Tunnel == null || ManagementClient == null)
        {
            return;
        }

        var updatedTunnel = await ManagementClient.GetTunnelAsync(
            Tunnel, new TunnelRequestOptions { IncludePorts = true });

        var updatedPorts = updatedTunnel?.Ports ?? Array.Empty<TunnelPort>();
        Tunnel.Ports = updatedPorts;

        var forwardTasks = new List<Task>();

        foreach (var port in updatedPorts)
        {
            foreach (var session in SshSessions
                .Where((s) => s.IsConnected && s.SessionId != null))
            {
                var key = new SessionPortKey(session.SessionId!, port.PortNumber);
                if (!RemoteForwarders.ContainsKey(key))
                {
                    // Overlapping refresh operations could cause duplicate forward requests to be
                    // sent to clients, but clients should ignore the duplicate requests.
                    var pfs = session.GetService<PortForwardingService>() !;
                    forwardTasks.Add(ForwardPortAsync(pfs, port, cancellation));
                }
            }
        }

        foreach (var forwarder in RemoteForwarders)
        {
            if (!updatedPorts.Any((p) => p.PortNumber == forwarder.Value.LocalPort))
            {
                // Since RemoteForwarders is a concurrent dictionary, overlapping refresh
                // operations will only be able to remove and dispose a forwarder once.
                if (RemoteForwarders.TryRemove(forwarder.Key, out _))
                {
                    forwarder.Value.Dispose();
                }
            }
        }

        await Task.WhenAll(forwardTasks);
    }

    /// <summary>
    /// Add client SSH session. Duplicates are ignored.
    /// Thread-safe.
    /// </summary>
    protected void AddClientSshSession(SshServerSession session)
    {
        Requires.NotNull(session, nameof(session));
        lock (this.sshSessions)
        {
            this.sshSessions.Add(session);
        }
    }

    /// <summary>
    /// Remove client SSH session. Noop if the session is not added.
    /// Thread-safe.
    /// </summary>
    protected void RemoveClientSshSession(SshServerSession session)
    {
        Requires.NotNull(session, nameof(session));
        lock (this.sshSessions)
        {
            this.sshSessions.Remove(session);
        }
    }
}
