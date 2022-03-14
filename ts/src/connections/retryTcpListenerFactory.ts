import { TcpListenerFactory } from '@vs/vs-ssh-tcp/tcpListenerFactory';
import { Server } from 'net';
import { CancellationToken } from 'vscode-jsonrpc';
import * as net from 'net';

/**
 * Implementation of a TCP listener factory that retries forwarding with nearby ports and falls back to a random port.
 * We make the assumption that the remote port that is being connected to and localPort numbers are the same.
 */
export class RetryTcpListenerFactory implements TcpListenerFactory {
    public async createTcpListener(
        localIPAddress: string,
        localPort: number,
        canChangePort: boolean,
        cancellation?: CancellationToken,
    ): Promise<Server> {
        if (!localIPAddress) throw new TypeError('Local IP address is required.');

        const maxOffet = 10;
        let listener = net.createServer();

        for (let offset = 0; ; offset++) {
            // After reaching the max offset, pass 0 to pick a random available port.
            let localPortNumber = offset === maxOffet ? 0 : localPort + offset;

            try {
                return await new Promise<Server>((resolve, reject) => {
                    listener.listen({
                        host: localIPAddress,
                        port: localPortNumber,
                        ipv6Only: net.isIPv6(localIPAddress),
                    });
                    listener.on('listening', () => {
                        resolve(listener);
                    });
                    listener.on('error', (err) => {
                        reject(err);
                    });
                });
            } catch (err) {
                console.log('Listening on port ' + localPortNumber + ' failed: ' + err);
                console.log('Incrementing port and trying again');
                continue;
            }
        }
    }
}
