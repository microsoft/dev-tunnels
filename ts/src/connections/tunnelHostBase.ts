// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

import { TunnelPort, Tunnel, TunnelAccessScopes } from '@microsoft/dev-tunnels-contracts';
import { TunnelManagementClient } from '@microsoft/dev-tunnels-management';
import {
    KeyPair,
    MultiChannelStream,
    SshAlgorithms,
    SshChannel,
    SshServerSession,
    SshStream,
    Trace,
} from '@microsoft/dev-tunnels-ssh';
import { PortForwardingService, RemotePortForwarder } from '@microsoft/dev-tunnels-ssh-tcp';
import { SessionPortKey } from './sessionPortKey';
import { TunnelConnectionSession } from './tunnelConnectionSession';
import { TunnelHost } from './tunnelHost';
import { tunnelSshSessionClass } from './tunnelSshSessionClass';
import { isNode } from './sshHelpers';
import { Emitter } from 'vscode-jsonrpc';
import { ForwardedPortConnectingEventArgs } from './forwardedPortConnectingEventArgs';

/**
 * Base class for Hosts that host one tunnel and use SSH MultiChannelStream to connect to the tunnel host service.
 */
export class TunnelHostBase
    extends tunnelSshSessionClass<MultiChannelStream>(TunnelConnectionSession)
    implements TunnelHost {
    /**
     * Sessions created between this host and clients
     * @internal
     */
    public sshSessions: SshServerSession[] = [];

    /**
     * Port Forwarders between host and clients
     */
    public remoteForwarders: { [sessionPortKey: string]: RemotePortForwarder } = {};

    /**
     * Private key used for connections.
     */
    public hostPrivateKey?: KeyPair;

    /**
     * Public keys used for connections.
     */
    public hostPublicKeys?: string[];

    /**
     * Promise task to get private key used for connections.
     */
    public hostPrivateKeyPromise?: Promise<KeyPair>;

    private loopbackIp = '127.0.0.1';

    private forwardConnectionsToLocalPortsValue: boolean = isNode();

    private readonly forwardedPortConnectingEmitter = new Emitter<
        ForwardedPortConnectingEventArgs
    >();

    constructor(managementClient: TunnelManagementClient, trace?: Trace) {
        super(TunnelAccessScopes.Host, trace, managementClient);
        const publicKey = SshAlgorithms.publicKey.ecdsaSha2Nistp384!;
        if (publicKey) {
            this.hostPrivateKeyPromise = publicKey.generateKeyPair();
        }
    }

    /**
     * An event which fires when a connection is made to the forwarded port.
     * Set forwardConnectionsToLocalPorts to false if a local TCP socket should not be created for the connection stream.
     * When this is set only the forwardedPortConnecting event will be raised.
     */
    public readonly forwardedPortConnecting = this.forwardedPortConnectingEmitter.event;

    /**
     * A value indicating whether the port-forwarding service forwards connections to local TCP sockets.
     * Forwarded connections are not possible if the host is not NodeJS (e.g. browser).
     * The default value for NodeJS hosts is true.
     */
    public get forwardConnectionsToLocalPorts(): boolean {
        return this.forwardConnectionsToLocalPortsValue;
    }

    public set forwardConnectionsToLocalPorts(value: boolean) {
        if (value === this.forwardConnectionsToLocalPortsValue) {
            return;
        }

        if (value && !isNode()) {
            throw new Error('Cannot forward connections to local TCP sockets on this platform.');
        }

        this.forwardConnectionsToLocalPortsValue = value;
    }

    /**
     * Connects to a tunnel as a host and starts accepting incoming connections
     * to local ports as defined on the tunnel.
     */
    public async start(tunnel: Tunnel): Promise<void> {
        await this.connectTunnelSession(tunnel);
    }

    public async refreshPorts(): Promise<void> {
        if (!this.tunnel || !this.managementClient) {
            return;
        }

        const updatedTunnel = await this.managementClient.getTunnel(this.tunnel, undefined);
        const updatedPorts = updatedTunnel?.ports ?? [];
        this.tunnel.ports = updatedPorts;

        const forwardPromises: Promise<any>[] = [];

        for (let port of updatedPorts) {
            for (let session of this.sshSessions.filter((s) => s.isConnected && s.sessionId)) {
                const key = new SessionPortKey(session.sessionId!, Number(port.portNumber));
                const forwarder = this.remoteForwarders[key.toString()];
                if (!forwarder) {
                    const pfs = session.getService(PortForwardingService)!;
                    forwardPromises.push(this.forwardPort(pfs, port));
                }
            }
        }

        for (let [key, forwarder] of Object.entries(this.remoteForwarders)) {
            if (!updatedPorts.some((p) => p.portNumber === forwarder.localPort)) {
                delete this.remoteForwarders[key];
                forwarder.dispose();
            }
        }

        await Promise.all(forwardPromises);
    }

    protected onForwardedPortConnecting(port: number, channel: SshChannel): void {
        const eventArgs = new ForwardedPortConnectingEventArgs(port, new SshStream(channel));
        this.forwardedPortConnectingEmitter.fire(eventArgs);
    }

    protected async forwardPort(pfs: PortForwardingService, port: TunnelPort) {
        let sessionId = pfs.session.sessionId;
        if (!sessionId) {
            throw new Error('No session id');
        }

        const portNumber = Number(port.portNumber);
        if (pfs.localForwardedPorts.find((p) => p.localPort === portNumber)) {
            // The port is already forwarded. This may happen if we try to add the same port twice after reconnection.
            return;
        }

        // When forwarding from a Remote port we assume that the RemotePortNumber
        // and requested LocalPortNumber are the same.
        const forwarder = await pfs.forwardFromRemotePort(
            this.loopbackIp,
            portNumber,
            this.loopbackIp,
            portNumber,
        );
        if (!forwarder) {
            // The forwarding request was rejected by the client.
            return;
        }

        const key = new SessionPortKey(sessionId, Number(forwarder.localPort));
        this.remoteForwarders[key.toString()] = forwarder;
    }

    /**
     * Validate the tunnel and get data needed to connect to it, if the tunnel is provided;
     * otherwise, ensure that there is already sufficient data to connect to a tunnel.
     * @param tunnel Tunnel to use for the connection.
     *     Tunnel object to get the connection data if defined.
     *     Undefined if the connection data is already known.
     * @internal
     */
    public async onConnectingToTunnel(tunnel?: Tunnel): Promise<void> {
        if (this.hostPrivateKey && this.hostPublicKeys) {
            return;
        }
        if (!tunnel) {
            throw new Error('Tunnel is required');
        }

        if (!this.hostPrivateKeyPromise) {
            throw new Error('Cannot create host keys');
        }
        this.hostPrivateKey = await this.hostPrivateKeyPromise;
        const buffer = await this.hostPrivateKey.getPublicKeyBytes(
            this.hostPrivateKey.keyAlgorithmName,
        );
        if (!buffer) {
            throw new Error('Host private key public key bytes is not initialized');
        }
        this.hostPublicKeys = [buffer.toString('base64')];
    }
}
