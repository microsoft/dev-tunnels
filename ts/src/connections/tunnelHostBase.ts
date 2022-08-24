// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

import { TunnelPort, Tunnel } from '@vs/tunnels-contracts';
import { TunnelManagementClient } from '@vs/tunnels-management';
import { KeyPair, SshAlgorithms, SshServerSession, Trace } from '@vs/vs-ssh';
import { PortForwardingService, RemotePortForwarder } from '@vs/vs-ssh-tcp';
import { SessionPortKey } from './sessionPortKey';
import { TunnelHost } from './tunnelHost';

/**
 * Base class for Hosts that host one tunnel
 */
export abstract class TunnelHostBase implements TunnelHost {
    /**
     * Sessions created between this host and clients
     */
    public sshSessions: SshServerSession[] = [];

    /**
     * Get the tunnel that is being hosted.
     */
    public tunnel?: Tunnel;

    /**
     * Port Forwarders between host and clients
     */
    public remoteForwarders: { [sessionPortKey: string]: RemotePortForwarder } = {};

    /**
     * Private key used for connections.
     */
    public hostPrivateKey?: KeyPair;

    /**
     * Promise task to get private key used for connections.
     */
    public hostPrivateKeyPromise?: Promise<KeyPair>;

    /**
     * Management client used for connections
     */
    public managementClient: TunnelManagementClient;

    /**
     * Trace used for writing output
     * @param level
     * @param eventId
     * @param msg
     * @param err
     */
    public trace: Trace = (level, eventId, msg, err) => {};

    private loopbackIp = '127.0.0.1';

    constructor(managementClient: TunnelManagementClient) {
        this.managementClient = managementClient;
        const publicKey = SshAlgorithms.publicKey.ecdsaSha2Nistp384!;
        if (publicKey) {
            this.hostPrivateKeyPromise = publicKey.generateKeyPair();
        }
    }

    /**
     * Do start work specific to the type of host.
     * @param tunnel
     * @param hostPublicKeys
     */
    protected abstract startServer(tunnel: Tunnel, hostPublicKeys?: string[]): Promise<void>;

    public async start(tunnel: Tunnel): Promise<void> {
        if (this.tunnel) {
            throw new Error(
                'Already hosting a tunnel. Use separate instances to host multiple tunnels.',
            );
        }

        if (this.hostPrivateKeyPromise) {
            const hostPrivateKey = await this.hostPrivateKeyPromise;
            this.hostPrivateKey = hostPrivateKey;
            const buffer = await hostPrivateKey.getPublicKeyBytes(hostPrivateKey.keyAlgorithmName);
            if (buffer) {
                let hostPublicKeys = [buffer.toString('base64')];
                await this.startServer(tunnel, hostPublicKeys);
            } else {
                throw new Error('Host private key public key bytes is not initialized');
            }
        }
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

    protected async forwardPort(pfs: PortForwardingService, port: TunnelPort): Promise<boolean> {
        let sessionId = pfs.session.sessionId;
        if (!sessionId) {
            throw new Error('No session id');
        }

        // When forwarding from a Remote port we assume that the RemotePortNumber
        // and requested LocalPortNumber are the same.
        let forwarder = await pfs.forwardFromRemotePort(
            this.loopbackIp,
            Number(port.portNumber),
            this.loopbackIp,
            Number(port.portNumber),
        );
        if (!forwarder) {
            // The forwarding request was rejected by the client.
            return false;
        }

        const key = new SessionPortKey(sessionId, Number(forwarder.localPort));
        this.remoteForwarders[key.toString()] = forwarder;
        return true;
    }

    public abstract dispose(): Promise<void>;
}
