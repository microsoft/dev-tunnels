import { Tunnel, TunnelPort } from '@vs/tunnels-contracts';
import { Disposable } from 'vscode-jsonrpc';

/**
 * Interface for a host capable of sharing local ports via
 * a tunnel and accepting tunneled connections to those ports.
 */
export interface TunnelHost extends Disposable {
    /**
     * Connects to a tunnel as a host and starts accepting incoming connections
     * to local ports as defined on the tunnel.
     * @param tunnel
     */
    start(tunnel: Tunnel): Promise<void>;

    /**
     * Adds and starts forwarding a port to a tunnel that is currently being hosted through Start
     * @param portToAdd
     */
    addPort(portToAdd: TunnelPort): Promise<TunnelPort>;

    /**
     * Removes a port to a tunnel that is currently being hosted through Start
     * @param portNumberToRemove
     */
    removePort(portNumberToRemove: number): Promise<boolean>;

    /**
     * Updates a port in a tunnel that is currently being hosted through Start
     * @param updatedPort
     */
    updatePort(updatedPort: TunnelPort): Promise<TunnelPort>;
}
