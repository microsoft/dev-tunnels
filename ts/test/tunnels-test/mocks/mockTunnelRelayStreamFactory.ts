// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

import { TunnelRelayStreamFactory } from '@vs/tunnels-connections';
import { Stream } from '@vs/vs-ssh';
import { access } from 'fs';
import { connection, IClientConfig } from 'websocket';

export class MockTunnelRelayStreamFactory implements TunnelRelayStreamFactory {
    private readonly connectionType: string;
    private readonly stream: Stream;

    constructor(connectionType: string, stream: Stream) {
        this.connectionType = connectionType;
        this.stream = stream;
    }

    public createRelayStream(
        relayUri: string,
        connectionType: string,
        accessToken?: string,
        clientConfig?: IClientConfig,
    ) {
        if (!relayUri || !access || this.connectionType !== connectionType) {
            throw new Error('Invalid params');
        }
        return Promise.resolve(this.stream);
    }
}
