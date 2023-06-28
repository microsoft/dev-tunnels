// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

import { Tunnel } from '@microsoft/dev-tunnels-contracts';

/**
 * Supports parsing tunnel access token JWT properties to allow for some pre-validation
 * and diagnostics.
 *
 * Applications generally should not attempt to interpret or rely on any token properties
 * other than `expiration`, because the service may change or omit those claims in the future.
 * Other claims are exposed here only for diagnostic purposes.
 */
export class TunnelAccessTokenProperties {
    private constructor(
        public readonly clusterId?: string,
        public readonly tunnelId?: string,
        public readonly tunnelPorts?: number[],
        public readonly scopes?: string[],
        public readonly issuer?: string,
        public readonly expiration?: Date,
    ) {}

    public toString(): string {
        let s = '';

        if (this.tunnelId) {
            s += 'tunnel=';
            s += this.tunnelId;

            if (this.clusterId) {
                s += '.';
                s += this.clusterId;
            }
        }

        if (this.tunnelPorts && this.tunnelPorts.length > 0) {
            if (s.length > 0) s += ', ';
            if (this.tunnelPorts.length === 1) {
                s += `port=${this.tunnelPorts[0]}`;
            } else {
                s += `ports=[${this.tunnelPorts.join(', ')}]`;
            }
        }

        if (this.scopes) {
            if (s.length > 0) s += ', ';
            s += `scopes=[${this.scopes.join(', ')}]`;
        }

        if (this.issuer) {
            if (s.length > 0) s += ', ';
            s += 'issuer=';
            s += this.issuer;
        }

        if (this.expiration) {
            if (s.length > 0) s += ', ';
            s += `expiration=${this.expiration.toString().replace('.000Z', 'Z')}`;
        }

        return s;
    }

    /**
     * Checks if the tunnel access token expiration claim is in the past.
     *
     * (Does not throw if the token is an invalid format.)
     */
    public static validateTokenExpiration(token: string): void {
        const t = TunnelAccessTokenProperties.tryParse(token);
        if (t?.expiration) {
            if (t.expiration < new Date()) {
                throw new Error('The access token is expired: ' + t);
            }
        }
    }

    /**
     * Attempts to parse a tunnel access token (JWT). This does NOT validate the token
     * signature or any claims.
     */
    public static tryParse(token: string): TunnelAccessTokenProperties | null {
        if (typeof token !== 'string') throw new TypeError('Token string expected.');

        // JWTs are encoded in 3 parts: header, body, and signature.
        const tokenParts = token.split('.');
        if (tokenParts.length !== 3) {
            return null;
        }

        const tokenBodyJson = TunnelAccessTokenProperties.base64UrlDecode(tokenParts[1]);
        if (!tokenBodyJson) {
            return null;
        }

        try {
            const tokenJson = JSON.parse(tokenBodyJson);
            const clusterId: string | undefined = tokenJson.clusterId;
            const tunnelId: string | undefined = tokenJson.tunnelId;
            const ports: number | number[] | undefined = tokenJson.tunnelPorts;
            const scp: string | undefined = tokenJson.scp;
            const iss: string | undefined = tokenJson.iss;
            const exp: number | undefined = tokenJson.exp;

            return new TunnelAccessTokenProperties(
                clusterId,
                tunnelId,
                typeof ports === 'number' ? [ports] : ports,
                scp?.split(' '),
                iss,
                typeof exp === 'number' ? new Date(exp * 1000) : undefined,
            );
        } catch {
            return null;
        }
    }

    /**
     * Gets the tunnal access token trace string.
     * 'none' if null or undefined, parsed token info if can be parsed, or 'token' if cannot be parsed.
     */
    public static getTokenTrace(token?: string | null | undefined): string {
        return !token ? 'none' : TunnelAccessTokenProperties.tryParse(token)?.toString() ?? 'token';
    }

    /**
     * Gets a tunnel access token that matches any of the provided access token scopes.
     * Validates token expiration if the token is found and throws an error if it's expired.
     * @param tunnel The tunnel to get the access tokens from.
     * @param accessTokenScopes What scopes the token needs to have.
     * @returns Tunnel access token if found; otherwise, undefined.
     */
    public static getTunnelAccessToken(
        tunnel?: Tunnel | null,
        accessTokenScopes?: string | string[],
    ): string | undefined {
        if (!tunnel?.accessTokens || !accessTokenScopes) {
            return;
        }

        if (!Array.isArray(accessTokenScopes)) {
            accessTokenScopes = [accessTokenScopes];
        }

        for (const scope of accessTokenScopes) {
            for (const [key, accessToken] of Object.entries(tunnel.accessTokens)) {
                // Each key may be either a single scope or space-delimited list of scopes.
                if (accessToken && key.split(' ').includes(scope)) {
                    TunnelAccessTokenProperties.validateTokenExpiration(accessToken);
                    return accessToken;
                }
            }
        }
    }

    private static base64UrlDecode(encodedString: string): string | null {
        // Convert from base64url encoding to base64 encoding: replace chars and add padding.
        encodedString = encodedString.replace('-', '+');
        while (encodedString.length % 4 !== 0) {
            encodedString += '=';
        }

        try {
            const result = atob(encodedString);
            return result;
        } catch {
            return null;
        }
    }
}
