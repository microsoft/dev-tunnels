// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

/**
 * Class for comparing equality in sessionId port pairs
 */
export class SessionPortKey {
    /**
     * Session Id from host
     */
    public sessionId: Buffer;

    /**
     * Port that is hosted client side
     */
    public port?: number;

    constructor(sessionId: Buffer, port: number) {
        this.sessionId = sessionId;
        this.port = port;
    }

    public equals(other: SessionPortKey) {
        return this.port === other.port && this.sessionId === other.sessionId;
    }

    public toString() {
        return this.port + '_' + this.sessionId.toString('base64');
    }
}
