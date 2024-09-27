// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

import { TcpListenerFactory } from '@microsoft/dev-tunnels-ssh-tcp/tcpListenerFactory';
import { Server } from 'net';
import { CancellationToken } from 'vscode-jsonrpc';
import * as net from 'net';

/**
 * Implementation of a TCP listener factory that retries forwarding with nearby ports and falls back to a random port.
 * We make the assumption that the remote port that is being connected to and localPort numbers are the same.
 */
export class RetryTcpListenerFactory implements TcpListenerFactory {
    public constructor(public readonly localAddress: string) {}

    public async createTcpListener(
        remotePort: number | undefined,
        localIPAddress: string,
        localPort: number,
        canChangeLocalPort: boolean,
        cancellation?: CancellationToken,
    ): Promise<Server> {
        // The SSH protocol may specify a local IP address for forwarding, but that is ignored
        // by tunnels. Instead, the tunnel client can specify the local IP address.
        if (localIPAddress.indexOf(':') >= 0) {
            // Convert special local address values from IPv4 to IPv6.
            if (this.localAddress === '0.0.0.0') {
                localIPAddress = '::';
            } else if (this.localAddress === '127.0.0.1') {
                localIPAddress = '::1';
            }
        } else {
            // IPv4
            localIPAddress = this.localAddress;
        }

        const maxOffset = 10;
        const listener = net.createServer();

        for (let offset = 0; ; offset++) {
            // After reaching the max offset, pass 0 to pick a random available port.
            const localPortNumber = offset === maxOffset ? 0 : localPort + offset;

            try {
                return await new Promise<Server>((resolve, reject) => {
                    listener.listen({
                        host: localIPAddress,
                        port: localPortNumber,
                        ipv6Only: net.isIPv6(localIPAddress),
                    });
                    listener.on('listening', () => {
                        if (remotePort) {
                            const { address, port } = <net.AddressInfo>listener.address();
                            console.log(`Forwarding from ${address}:${port} to host port ${remotePort}.`);
                        }

                        resolve(listener);
                    });
                    listener.on('error', (err) => {
                        reject(err);
                    });
                });
            } catch (err) {
                if (offset < maxOffset && canChangeLocalPort) {
                    console.log('Listening on port ' + localPortNumber + ' failed: ' + err);
                    console.log('Incrementing port and trying again');
                    continue;
                } else {
                    throw err;
                }
            }
        }
    }
}
