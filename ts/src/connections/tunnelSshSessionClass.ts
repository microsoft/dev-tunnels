// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

import {
    ObjectDisposedError,
    SshChannelError,
    SshConnectionError,
    SshDisconnectReason,
    SshReconnectError,
} from '@vs/vs-ssh';
import { TunnelSession } from './tunnelSession';
import { TunnelConnection } from './tunnelConnection';
import { TunnelConnectionSession } from './tunnelConnectionSession';
import { Tunnel } from '@vs/tunnels-contracts';

type Constructor<T = {}> = new (...args: any[]) => T;
type CloseableSshSession = {
    readonly isClosed: boolean;
    close(reason?: SshDisconnectReason, message?: string, error?: Error): Promise<void>;
    dispose(): void;
};

/**
 * Tunnel relay mixin class that adds sshSession property and implements closeSession().
 * The class's dispose() closes the session, and onConnectingToTunnel()
 * throws if the session already exists when connecting to a new tunnel.
 * @param base Base class constructor.
 * @returns A class with sshSession property where closeSession() closes it and dispose() calls closeSession().
 */
export function tunnelSshSessionClass<
    TSshSession extends CloseableSshSession,
    TBase extends Constructor<TunnelSession & TunnelConnection> = Constructor<
        TunnelConnectionSession
    >
>(base: TBase) {
    return class SshTunnelSession extends base {
        /**
         * SSH session that is used to connect to the tunnel.
         */
        public sshSession?: TSshSession;

        /**
         * Closes the tunnel SSH session.
         */
        public async closeSession(error?: Error): Promise<void> {
            await super.closeSession(error);
            if (!this.sshSession) {
                return;
            }

            if (!this.sshSession.isClosed) {
                let reason = SshDisconnectReason.byApplication;
                if (error) {
                    if (error instanceof SshConnectionError) {
                        reason =
                            (error as SshConnectionError).reason ??
                            SshDisconnectReason.connectionLost;
                    } else if (
                        error instanceof SshReconnectError ||
                        error instanceof SshChannelError
                    ) {
                        reason = SshDisconnectReason.protocolError;
                    } else {
                        reason = SshDisconnectReason.connectionLost;
                    }
                }
                await this.sshSession.close(reason, undefined, error);
            }

            // Closing the SSH session does nothing if the session is in disconnected state,
            // which may happen for a reconnectable session when the connection drops.
            // Disposing of the session forces closing and frees up the resources.
            if (this.sshSession) {
                this.sshSession.dispose();
                this.sshSession = undefined;
            }
        }

        /**
         * Disposes this tunnel session, closing the SSH session used for it.
         */
        public async dispose(): Promise<void> {
            await super.dispose();
            try {
                await this.closeSession(this.disconnectError);
            } catch (e) {
                if (!(e instanceof ObjectDisposedError)) throw e;
            }
        }

        /**
         * Validate the tunnel and get data needed to connect to it, if the tunnel is provided;
         * otherwise, ensure that there is already sufficient data to connect to a tunnel.
         * @param tunnel Tunnel to use for the connection.
         *     Tunnel object to get the connection data if defined.
         *     Undefined if the connection data is already known.
         */
        public async onConnectingToTunnel(tunnel?: Tunnel): Promise<void> {
            await super.onConnectingToTunnel(tunnel);
            if (tunnel && this.sshSession) {
                throw new Error(
                    'Already connected. Use separate instances to connect to multiple tunnels.',
                );
            }
        }
    };
}
