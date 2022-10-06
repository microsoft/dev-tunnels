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
    public clusterId?: string;
    public tunnelId?: string;
    public tunnelPort?: number;
    public scp?: string;
    public iss?: string;
    public exp?: number;

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

        if (typeof this.tunnelPort === 'number') {
            if (s.length > 0) s += ', ';
            s += 'port=';
            s += this.tunnelPort;
        }

        if (this.scp) {
            if (s.length > 0) s += ', ';
            const scopes = this.scp.split(' ');
            s += `scopes=[${scopes.join(', ')}]`;
        }

        if (this.iss) {
            if (s.length > 0) s += ', ';
            s += 'issuer=';
            s += this.iss;
        }

        if (this.exp) {
            if (s.length > 0) s += ', ';
            const expiration = new Date(this.exp * 1000);
            s += 'expiration=';
            s += expiration.toString().replace('.000Z', 'Z');
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
        if (typeof t?.exp === 'number') {
            const expiration = new Date(t.exp * 1000);
            if (expiration < new Date()) {
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
            const result = new TunnelAccessTokenProperties();
            Object.assign(result, JSON.parse(tokenBodyJson));
            return result;
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
        tunnel?: Tunnel,
        accessTokenScopes?: string | string[],
    ): string | undefined {
        if (!tunnel?.accessTokens || !accessTokenScopes) {
            return;
        }

        if (!Array.isArray(accessTokenScopes)) {
            accessTokenScopes = [accessTokenScopes];
        }

        for (let scope of accessTokenScopes) {
            for (let [key, accessToken] of Object.entries(tunnel.accessTokens)) {
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
