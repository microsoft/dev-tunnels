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
using Microsoft.VisualStudio.Ssh;
using Microsoft.VisualStudio.Ssh.Algorithms;
using Microsoft.VisualStudio.Ssh.Tcp;
using Microsoft.VsSaaS.TunnelService.Contracts;

namespace Microsoft.VsSaaS.TunnelService
{
    /// <summary>
    /// Base class for Hosts that host one tunnel
    /// </summary>
    public abstract class TunnelHostBase : TunnelBase, ITunnelHost
    {
        /// <summary>
        /// Sessions created between this host and clients. Lock on this hash set to be thread-safe.
        /// </summary>
        protected HashSet<SshServerSession> sshSessions = new();

        /// <summary>
        /// Creates a new instance of the <see cref="TunnelHostBase" /> class.
        /// </summary>
        public TunnelHostBase(ITunnelManagementClient managementClient, TraceSource trace) : base(managementClient, trace)
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

        /// <inheritdoc />
        public async Task<TunnelPort> AddPortAsync(
            TunnelPort portToAdd,
            CancellationToken cancellation)
        {
            if (Tunnel == null)
            {
                throw new InvalidOperationException("Tunnel must be running");
            }

            var port = await ManagementClient!.CreateTunnelPortAsync(Tunnel, portToAdd, null, cancellation);
            await Task.WhenAll(SshSessions.Select(sshSession => Task.Run(async () =>
            {
                if (sshSession.Principal == null || !sshSession.IsConnected)
                {
                    // Two possible cases:
                    // - The session is not yet authenticated; all ports will be forwarded after the session is authenticated.
                    // - The session is currently disconnected and will reconnect; all ports will be re-forwarded after the session is reconnected.
                    return;
                }

                var sessionId = sshSession.SessionId;
                var pfs = sshSession.GetService<PortForwardingService>();
                if (pfs == null)
                {
                    throw new InvalidOperationException("PFS must be active to add ports");
                }

                await ForwardPortAsync(pfs, port, cancellation);
            })));

            return port;
        }

        /// <inheritdoc />
        public async Task<bool> RemovePortAsync(
            ushort portNumberToRemove,
            CancellationToken cancellation)
        {
            if (Tunnel == null || Tunnel.Ports == null)
            {
                throw new InvalidOperationException("Tunnel must be running and have ports to delete");
            }

            var portDeleted = await ManagementClient!.DeleteTunnelPortAsync(Tunnel, portNumberToRemove, null, cancellation);

            Parallel.ForEach(SshSessions, sshSession =>
            {
                var sessionId = sshSession.SessionId;
                if (sessionId != null && sshSession.IsConnected)
                {
                    foreach (KeyValuePair<SessionPortKey, RemotePortForwarder> entry in RemoteForwarders)
                    {
                        if (entry.Value.LocalPort == portNumberToRemove && Enumerable.SequenceEqual(entry.Key.SessionId, sessionId))
                        {
                            var remoteForwarderFound = RemoteForwarders.TryRemove(entry.Key, out var remoteForwarder);
                            if (remoteForwarderFound && remoteForwarder != null)
                            {
                                remoteForwarder.Dispose();
                            }

                            break;
                        }
                    }
                }
            });

            return portDeleted;
        }

        /// <inheritdoc />
        public async Task<TunnelPort> UpdatePortAsync(
            TunnelPort updatedPort,
            CancellationToken cancellation)
        {
            if (Tunnel == null || Tunnel.Ports == null)
            {
                throw new InvalidOperationException("Tunnel must be running and have ports to update");
            }

            var port = await ManagementClient!.UpdateTunnelPortAsync(Tunnel, updatedPort, null, cancellation);
            return port;
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
            var forwarder = await pfs.ForwardFromRemotePortAsync(
                IPAddress.Loopback,
                portNumber,
                IPAddress.Loopback.ToString(),
                portNumber,
                cancellation);

            if (forwarder == null)
            {
                // The forwarding request was rejected by the client.
                return;
            }

            RemoteForwarders.TryAdd(
                new SessionPortKey(sessionId, (ushort)forwarder.RemotePort),
                forwarder);
            return;
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
}
