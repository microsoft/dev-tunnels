// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

import {
    ObjectDisposedError,
    SshChannelError,
    SshConnectionError,
    SshDisconnectReason,
    SshReconnectError,
} from '@microsoft/dev-tunnels-ssh';
import { TunnelSession } from './tunnelSession';
import { TunnelConnection } from './tunnelConnection';
import { TunnelConnectionSession } from './tunnelConnectionSession';

type Constructor<T = object> = new (...args: any[]) => T;
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
         * @internal
         */
        public sshSession?: TSshSession;

        /**
         * Closes the tunnel SSH session.
         * @internal
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
    };
}
