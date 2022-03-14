/**
 * Defines scopes for tunnel access tokens.
 */
export class TunnelAccessScopes {
    /**
     * Allows management operations on tunnels and tunnel ports.
     */
    public static readonly manage = 'manage';

    /**
     * Allows accepting connections on tunnels as a host.
     */
    public static readonly host = 'host';

    /**
     * Allows inspecting tunnel connection activity and data.
     */
    public static readonly inspect = 'inspect';

    /**
     * Allows connecting to tunnels as a client.
     */
    public static readonly connect = 'connect';

    /**
     * Array of all access scopes.
     */
    public static readonly all = [this.manage, this.host, this.inspect, this.connect];

    /**
     * Checks that all items in an array of scopes are valid.
     */
    public static validate(scopes: string[], validScopes?: string[]) {
        if (scopes == null) {
            throw new Error('Argument invalid: scopes');
        }

        scopes.forEach((scope) => {
            if (!scope) {
                throw new Error('Tunnel access scopes include a null/empty item.');
            } else if (!TunnelAccessScopes.all.includes(scope)) {
                throw new Error('Invalid tunnel access scope: ' + scope);
            }
        });

        if (validScopes) {
            scopes.forEach((scope) => {
                if (!validScopes.includes(scope)) {
                    throw new Error('Tunnel access scope is invalid for current request: scope');
                }
            });
        }
    }
}
