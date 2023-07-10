// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

/**
 * Class for comparing equality in sessionId port pairs
 */
export class SessionPortKey {
    /**
     * Session ID of the client SSH session, or null if the session does not have an ID
     * (because it is not encrypted and not client-specific).
     */
    public sessionId: Buffer | null;

    /**
     * Forwarded port number
     */
    public port: number;

    public constructor(sessionId: Buffer | null, port: number) {
        this.sessionId = sessionId ?? null;
        this.port = port;
    }

    public equals(other: SessionPortKey) {
        return this.port === other.port &&
            ((!this.sessionId && !other.sessionId) ||
                this.sessionId && other.sessionId && this.sessionId === other.sessionId);
    }

    public toString() {
        return this.port + (this.sessionId ? '_' + this.sessionId.toString('base64') : '');
    }
}
