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
using Microsoft.DevTunnels.Ssh.Tcp;
using Microsoft.DevTunnels.Contracts;
using Microsoft.DevTunnels.Management;
using Microsoft.DevTunnels.Ssh.Tcp.Events;

namespace Microsoft.DevTunnels.Connections;

/// <summary>
/// Base class for Hosts that host one tunnel
/// </summary>
public abstract class TunnelHost : TunnelRelayConnection, ITunnelHost
{
    internal const string RefreshPortsRequestType = "RefreshPorts";
    private bool forwardConnectionsToLocalPorts = true;

    /// <summary>
    /// Sessions created between this host and clients. Lock on this hash set to be thread-safe.
    /// </summary>
    protected HashSet<SshServerSession> sshSessions = new();

    /// <inheritdoc />
    public event EventHandler<ForwardedPortConnectingEventArgs>? ForwardedPortConnecting;

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
    /// <remarks>
    /// This property is public for testing purposes, and may be removed in the future.
    /// </remarks>
    public ConcurrentDictionary<SessionPortKey, RemotePortForwarder> RemoteForwarders { get; }
        = new ConcurrentDictionary<SessionPortKey, RemotePortForwarder>();

    /// <summary>
    /// Private key used for connections.
    /// </summary>
    protected IKeyPair HostPrivateKey { get; } = SshAlgorithms.PublicKey.ECDsaSha2Nistp384.GenerateKeyPair();

    /// <inheritdoc />
    protected override string TunnelAccessScope => TunnelAccessScopes.Host;

    /// <inheritdoc />
    public bool ForwardConnectionsToLocalPorts
    {
        get => this.forwardConnectionsToLocalPorts;
        set
        {
            if (value != this.forwardConnectionsToLocalPorts)
            {
                this.forwardConnectionsToLocalPorts = value;
            }
        }
    }

    /// <inheritdoc />
    [Obsolete("Use ConnectAsync() instead.")]
    public Task StartAsync(Tunnel tunnel, CancellationToken cancellation)
        => ConnectAsync(tunnel, cancellation);

    /// <inheritdoc />
    public override async Task ConnectAsync(
        Tunnel tunnel,
        TunnelConnectionOptions? options,
        CancellationToken cancellation = default)
    {
        Requires.NotNull(tunnel, nameof(tunnel));
        Requires.NotNull(tunnel.Ports!, nameof(tunnel.Ports));
        await ConnectTunnelSessionAsync(tunnel, options, cancellation);
    }

    internal async Task ForwardPortAsync(
        PortForwardingService pfs,
        TunnelPort port,
        CancellationToken cancellation)
    {
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
                "localhost",
                portNumber,
                cancellation);
        }
        catch (SshConnectionException)
        {
            // Ignore exception caused by the session being closed; it will be reported elsewhere.
            // Treat it as equivalent to the client rejecting the forwarding request.
            forwarder = null;
        }
        catch (ObjectDisposedException)
        {
            forwarder = null;
        }

        if (forwarder == null)
        {
            // The forwarding request was rejected by the relay (V2) or client (V1).
            return;
        }

        // Capture the remote forwarder for the session id / remote port pair.
        // This is needed later to stop forwarding for this port when the remote forwarder is disposed.
        // Note when the client tries to open an SSH channel to PFS, the port forwarding service checks
        // its remoteConnectors whether the port is being forwarded.
        // Disposing of the RemotePortForwarder stops the forwarding and removes the remote connector
        // from PFS.remoteConnectors.
        //
        // Note the session ID may be null here in V2 protocol, when one (possibly unencrypted)
        // session is shared by all clients.
        RemoteForwarders.TryAdd(
            new SessionPortKey(pfs.Session.SessionId, (ushort)forwarder.LocalPort),
            forwarder);
        return;
    }

    /// <inheritdoc />
    public abstract Task RefreshPortsAsync(CancellationToken cancellation);

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
    /// Invoked when a forwarded port is connecting
    /// </summary>
    protected virtual void OnForwardedPortConnecting(
        object? sender, ForwardedPortConnectingEventArgs e)
    {
        this.ForwardedPortConnecting?.Invoke(this, e);
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
