// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

import { TunnelPort, Tunnel, TunnelAccessScopes } from '@microsoft/dev-tunnels-contracts';
import { TunnelManagementClient } from '@microsoft/dev-tunnels-management';
import {
    KeyPair,
    SshAlgorithms,
    SshClientSession,
    SshServerSession,
    Trace,
} from '@microsoft/dev-tunnels-ssh';
import { PortForwardingService, RemotePortForwarder } from '@microsoft/dev-tunnels-ssh-tcp';
import { SessionPortKey } from './sessionPortKey';
import { TunnelConnectionSession } from './tunnelConnectionSession';
import { TunnelHost } from './tunnelHost';
import { tunnelSshSessionClass } from './tunnelSshSessionClass';
import { isNode } from './sshHelpers';

/**
 * Base class for Hosts that host one tunnel and use SSH to connect to the tunnel host service.
 */
export class TunnelHostBase
    extends tunnelSshSessionClass<SshClientSession>(TunnelConnectionSession)
    implements TunnelHost {

    /**
     * Sessions created between this host and clients
     * @internal
     */
    public readonly sshSessions: SshServerSession[] = [];

    /**
     * Port Forwarders between host and clients
     */
    public readonly remoteForwarders = new Map<string, RemotePortForwarder>();

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

    public constructor(managementClient: TunnelManagementClient, trace?: Trace) {
        super(TunnelAccessScopes.Host, trace, managementClient);
        const publicKey = SshAlgorithms.publicKey.ecdsaSha2Nistp384!;
        if (publicKey) {
            this.hostPrivateKeyPromise = publicKey.generateKeyPair();
        }
    }

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

    public refreshPorts(): Promise<void> {
        // This is implemented by the derived class.
        return Promise.resolve();
    }

    protected async forwardPort(pfs: PortForwardingService, port: TunnelPort) {
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

        const key = new SessionPortKey(pfs.session.sessionId, Number(forwarder.localPort));
        this.remoteForwarders.set(key.toString(), forwarder);
    }

    /**
     * Validate the {@link tunnel} and get data needed to connect to it, if the tunnel is provided;
     * otherwise, ensure that there is already sufficient data to connect to a tunnel.
     * @internal
     */
    public async onConnectingToTunnel(): Promise<void> {
        if (this.hostPrivateKey && this.hostPublicKeys) {
            return;
        }
        if (!this.tunnel) {
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
