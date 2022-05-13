// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

import { Tunnel, TunnelConnectionMode, TunnelEndpoint } from '@vs/tunnels-contracts';
import {
    CancellationToken,
    SessionRequestMessage,
    SshAuthenticatingEventArgs,
    SshClientCredentials,
    SshClientSession,
    SshDisconnectReason,
    SshRequestEventArgs,
    SshSessionClosedEventArgs,
    SshSessionConfiguration,
    SshStream,
    Stream,
    Trace,
} from '@vs/vs-ssh';
import { ForwardedPortsCollection, PortForwardingService } from '@vs/vs-ssh-tcp';
import { RetryTcpListenerFactory } from './retryTcpListenerFactory';
import { isNode } from './sshHelpers';
import { TunnelClient } from './tunnelClient';
import { List } from './utils';
import { Emitter } from 'vscode-jsonrpc';

/**
 * Base class for clients that connect to a single host
 */
export abstract class TunnelClientBase implements TunnelClient {
    private readonly sshSessionClosedEmitter = new Emitter<this>();
    private acceptLocalConnectionsForForwardedPortsValue: boolean = isNode();
    /**
     * Session used to connect to host
     */
    public sshSession?: SshClientSession;

    public connectionModes: TunnelConnectionMode[] = [];

    protected get isSshSessionActive(): boolean {
        return !!this.sshSession?.isConnected;
    }

    protected readonly sshSessionClosed = this.sshSessionClosedEmitter.event;

    /**
     * Get a value indicating if remote port is forwarded and has any channels open on the client,
     * whether used by local tcp listener if {AcceptLocalConnectionsForForwardedPorts} is true, or
     * streamed via <see cref="ConnectToForwardedPortAsync(int, CancellationToken)"/>.
     */
    protected hasForwardedChannels(port: number): boolean {
        if (!this.isSshSessionActive) {
            return false;
        }

        const pfs = this.sshSession?.activateService(PortForwardingService);
        const remoteForwardedPorts = pfs?.remoteForwardedPorts;
        const forwardedPort = remoteForwardedPorts?.find((p) => p.remotePort === port);
        return !!forwardedPort && remoteForwardedPorts!.getChannels(forwardedPort).length > 0;
    }

    /**
     * A value indicating whether local connections for forwarded ports are accepted.
     * Local connections are not accepted if the host is not NodeJS (e.g. browser).
     */
    public get acceptLocalConnectionsForForwardedPorts(): boolean {
        return this.acceptLocalConnectionsForForwardedPortsValue;
    }

    public set acceptLocalConnectionsForForwardedPorts(value: boolean) {
        if (value === this.acceptLocalConnectionsForForwardedPortsValue) {
            return;
        }

        if (value && !isNode()) {
            throw new Error(
                'Cannot accept local connections for forwarded ports on this platform.',
            );
        }

        this.acceptLocalConnectionsForForwardedPortsValue = value;
        this.activatePfsIfNeeded();
    }

    /**
     * Trace to write output to console
     * @param level
     * @param eventId
     * @param msg
     * @param err
     */
    public trace: Trace = (level, eventId, msg, err) => {};

    public get forwardedPorts(): ForwardedPortsCollection | undefined {
        let pfs = this.sshSession?.activateService(PortForwardingService);
        return pfs?.remoteForwardedPorts;
    }

    constructor() {}

    protected abstract connectClient(tunnel: Tunnel, endpoints: TunnelEndpoint[]): Promise<void>;

    public async connect(tunnel: Tunnel, hostId?: string): Promise<void> {
        if (!tunnel) {
            throw new Error('Tunnel cannot be null');
        }
        if (!tunnel.endpoints) {
            throw new Error('Tunnel endpoints cannot be null');
        }

        if (this.sshSession) {
            throw new Error(
                'Already connected. Use separate instances to connect to multiple tunnels.',
            );
        }

        if (tunnel.endpoints.length === 0) {
            throw new Error('No hosts are currently accepting connections for the tunnel.');
        }

        let endpointGroups = List.groupBy(
            tunnel.endpoints,
            (endpoint: TunnelEndpoint) => endpoint.hostId,
        );
        let endpointGroup: TunnelEndpoint[] | undefined;
        if (hostId) {
            endpointGroup = endpointGroups.get(hostId);
            if (!endpointGroup) {
                throw new Error(
                    'The specified host is not currently accepting connections to the tunnel.',
                );
            }
        } else if (endpointGroups.size > 1) {
            throw new Error(
                'There are multiple hosts for the tunnel. Specify a host ID to connect to.',
            );
        } else {
            endpointGroup = endpointGroups.entries().next().value[1];
        }

        await this.connectClient(tunnel, endpointGroup!);
    }

    private onRequest(e: SshRequestEventArgs<SessionRequestMessage>) {
        if (
            e.request.requestType === PortForwardingService.portForwardRequestType ||
            e.request.requestType === PortForwardingService.cancelPortForwardRequestType
        ) {
            e.isAuthorized = true;
        }
    }

    public async startSshSession(stream: Stream): Promise<void> {
        let clientConfig = new SshSessionConfiguration();

        // Enable port-forwarding via the SSH protocol.
        clientConfig.addService(PortForwardingService);

        this.sshSession = new SshClientSession(clientConfig);
        this.sshSession.trace = this.trace;
        this.sshSession.onClosed((e) => this.onSshSessionClosed(e));
        this.sshSession.onAuthenticating((e) => this.onSshServerAuthenticating(e));

        this.activatePfsIfNeeded();

        this.sshSession.onRequest((e) => this.onRequest(e));

        await this.sshSession.connect(stream);

        // For now, the client is allowed to skip SSH authentication;
        // they must have a valid tunnel access token already to get this far.
        let clientCredentials: SshClientCredentials = {
            username: 'tunnel',
            password: undefined,
        };
        await this.sshSession.authenticate(clientCredentials);
    }

    private activatePfsIfNeeded() {
        if (!this.sshSession) {
            return;
        }

        const pfs = this.sshSession.activateService(PortForwardingService);
        // Do not start forwarding local connections for browser client connections or if this is not allowed.
        if (this.acceptLocalConnectionsForForwardedPortsValue && isNode()) {
            pfs.tcpListenerFactory = new RetryTcpListenerFactory();
        } else {
            pfs.acceptLocalConnectionsForForwardedPorts = false;
        }
    }

    public async connectToForwardedPort(
        fowardedPort: number,
        cancellation?: CancellationToken,
    ): Promise<SshStream> {
        const pfs = this.getSshSessionPfs();
        if (!pfs) {
            throw new Error(
                'Failed to connect to remote port. Ensure that the client has connected by calling connectClient.',
            );
        }
        return pfs.connectToForwardedPort(fowardedPort, cancellation);
    }

    public async waitForForwardedPort(
        forwardedPort: number,
        cancellation?: CancellationToken,
    ): Promise<void> {
        const pfs = this.getSshSessionPfs();
        if (!pfs) {
            throw new Error(
                'Port forwarding has not been started. Ensure that the client has connected by calling connectClient.',
            );
        }
        return pfs.waitForForwardedPort(forwardedPort, cancellation);
    }

    private getSshSessionPfs() {
        return this.sshSession?.getService(PortForwardingService) ?? undefined;
    }

    private onSshServerAuthenticating(e: SshAuthenticatingEventArgs): void {
        // TODO: Validate host public keys match those published to the service?
        // For now, the assumption is only a host with access to the tunnel can get a token
        // that enables listening for tunnel connections.
        e.authenticationPromise = Promise.resolve({});
    }

    private onSshSessionClosed(e: SshSessionClosedEventArgs) {
        this.sshSessionClosedEmitter.fire(this);
    }

    public async dispose(): Promise<void> {
        if (this.sshSession) {
            await this.sshSession.close(SshDisconnectReason.byApplication);
        }
    }
}
