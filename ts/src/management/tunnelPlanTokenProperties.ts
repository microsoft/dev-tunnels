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
export class TunnelPlanTokenProperties {
    private constructor(
        public readonly clusterId?: string,
        public readonly issuer?: string,
        public readonly expiration?: Date,
        public readonly userEmail?: string,
        public readonly tunnelPlanId?: string,
        public readonly subscriptionId?: string,
        public readonly scopes?: string[],
    ) {}

    /**
     * Checks if the tunnel access token expiration claim is in the past.
     * Note: uses client's system time for the validation.
     * (Does not throw if the token is an invalid format.)
     */
    public static validateTokenExpiration(token: string): void {
        const t = TunnelPlanTokenProperties.tryParse(token);
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
    public static tryParse(token: string): TunnelPlanTokenProperties | null {
        if (typeof token !== 'string') throw new TypeError('Token string expected.');

        // JWTs are encoded in 3 parts: header, body, and signature.
        const tokenParts = token.split('.');
        if (tokenParts.length !== 3) {
            return null;
        }

        const tokenBodyJson = TunnelPlanTokenProperties.base64UrlDecode(tokenParts[1]);
        if (!tokenBodyJson) {
            return null;
        }

        try {
            const tokenJson = JSON.parse(tokenBodyJson);
            const clusterId: string | undefined = tokenJson.clusterId;
            const subscriptionId: string | undefined = tokenJson.subscriptionId;
            const tunnelPlanId: string | undefined = tokenJson.tunnelPlanId;
            const userEmail: string | undefined = tokenJson.userEmail;
            const scp: string | undefined = tokenJson.scp;
            const iss: string | undefined = tokenJson.iss;
            const exp: number | undefined = tokenJson.exp;

            return new TunnelPlanTokenProperties(
                clusterId,
                iss,
                typeof exp === 'number' ? new Date(exp * 1000) : undefined,
                userEmail,
                tunnelPlanId,
                subscriptionId,
                scp?.split(' '),
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
        return !token ? 'none' : TunnelPlanTokenProperties.tryParse(token)?.toString() ?? 'token';
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
