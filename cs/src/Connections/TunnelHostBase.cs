// <copyright file="TunnelHostBase.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
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
    public abstract class TunnelHostBase : ITunnelHost
    {
        /// <summary>
        /// Creates a new instance of the <see cref="TunnelHostBase" /> class.
        /// </summary>
        public TunnelHostBase(ITunnelManagementClient managementClient, TraceSource trace)
        {
            ManagementClient = Requires.NotNull(managementClient, nameof(managementClient));
            Trace = Requires.NotNull(trace, nameof(trace));
        }

        /// <summary>
        /// Sessions created between this host and clients
        /// </summary>
        protected ConcurrentBag<SshServerSession> SshSessions { get; } = new ConcurrentBag<SshServerSession>();

        /// <summary>
        /// Get the tunnel that is being hosted.
        /// </summary>
        public Tunnel? Tunnel { get; protected set; }

        /// <summary>
        /// Port Forwarders between host and clients
        /// </summary>
        public ConcurrentDictionary<SessionPortKey, RemotePortForwarder> RemoteForwarders { get; } = new ConcurrentDictionary<SessionPortKey, RemotePortForwarder>();

        /// <summary>
        /// Private key used for connections.
        /// </summary>
        protected IKeyPair HostPrivateKey { get; } = SshAlgorithms.PublicKey.ECDsaSha2Nistp384.GenerateKeyPair();

        /// <summary>
        /// Management client used for connections
        /// </summary>
        protected ITunnelManagementClient ManagementClient { get; }

        /// <summary>
        /// Trace used for writing output
        /// </summary>
        protected TraceSource Trace { get; }

        /// <inheritdoc />
        public abstract ValueTask DisposeAsync();

        /// <summary>
        /// Do start work specific to the type of host.
        /// </summary>
        protected abstract Task StartAsync(Tunnel tunnel, string[]? hostPublicKeys, CancellationToken cancellation);

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
            var hostPublicKeys = new[]
            {
                HostPrivateKey.GetPublicKeyBytes(HostPrivateKey.KeyAlgorithmName).ToBase64(),
            };

            await StartAsync(tunnel, hostPublicKeys, cancellation);
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

            var port = await ManagementClient.CreateTunnelPortAsync(Tunnel, portToAdd, null, cancellation);
            await Task.WhenAll(SshSessions.Select(sshSession => Task.Run(async () =>
            {
                if (sshSession.Principal == null)
                {
                    // The session is not yet authenticated; all ports will be forwarded after
                    // the session is authenticated.
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

            var portDeleted = await ManagementClient.DeleteTunnelPortAsync(Tunnel, portNumberToRemove, null, cancellation);

            Parallel.ForEach(SshSessions, sshSession =>
            {
                var sessionId = sshSession.SessionId;
                if (sessionId != null)
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

            var port = await ManagementClient.UpdateTunnelPortAsync(Tunnel, updatedPort, null, cancellation);
            return port;
        }

        internal async Task<bool> ForwardPortAsync(
            PortForwardingService pfs,
            TunnelPort port,
            CancellationToken cancellation)
        {
            try{
                var sessionId = Requires.NotNull(pfs.Session.SessionId!, nameof(pfs.Session.SessionId));
                Requires.Argument(port.PortNumber.HasValue, nameof(port.PortNumber), "A port is required.");

                // When forwarding from a Remote port we assume that the RemotePortNumber
                // and requested LocalPortNumber are the same.
                var forwarder = await pfs.ForwardFromRemotePortAsync(
                    IPAddress.Loopback,
                    (int)port.PortNumber,
                    IPAddress.Loopback.ToString(),
                    (int)port.PortNumber,
                    cancellation);
                if (forwarder == null)
                {
                    // The forwarding request was rejected by the client.
                    return false;
                }

                RemoteForwarders.TryAdd(
                    new SessionPortKey(sessionId, (ushort)forwarder.RemotePort),
                    forwarder);
                return true;
            }
            catch(Exception ex)
            {
                Trace.TraceEvent(TraceEventType.Error, 0, "Error running client SSH session: {0}", ex.Message);
                return false;
            }
            
        }
    }
}
