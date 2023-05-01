// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

import { TunnelRelayStreamFactory, TunnelRelayTunnelHost } from '@microsoft/dev-tunnels-connections';
import {
    MultiChannelStream,
    NodeStream,
    PromiseCompletionSource,
    SshStream,
    Stream,
} from '@microsoft/dev-tunnels-ssh';
import { IClientConfig } from 'websocket';

export class MockTunnelRelayStreamFactory implements TunnelRelayStreamFactory {
    private readonly connectionType: string;
    private readonly stream: Stream;

    constructor(
        connectionType: string,
        stream: Stream,
        clientStreamFactory?: (stream: Stream) => Promise<Stream>,
    ) {
        this.connectionType = connectionType;
        this.stream = stream;
        if (clientStreamFactory) {
            this.createRelayStream = () => clientStreamFactory(this.stream);
        }
    }

    public createRelayStream = (
        relayUri: string,
        protocols: string[],
        accessToken?: string,
        clientConfig?: IClientConfig,
    ) => {
        if (!relayUri || !accessToken || !protocols.includes(this.connectionType)) {
            throw new Error('Invalid params');
        }
        return Promise.resolve(this.stream);
    };

    public static from(
        source: Stream | PromiseLike<Stream> | PromiseCompletionSource<Stream>,
    ): TunnelRelayStreamFactory {
        return {
            createRelayStream: async () => {
                return await (source instanceof PromiseCompletionSource
                    ? (<PromiseCompletionSource<Stream>>source).promise
                    : Promise.resolve(source));
            },
        };
    }

    public static fromMultiChannelStream(
        source:
            | MultiChannelStream
            | PromiseLike<MultiChannelStream>
            | PromiseCompletionSource<MultiChannelStream>,
        onClientChannelOpened?: (sshChannelStream: SshStream) => void,
        channelType?: string,
    ): TunnelRelayStreamFactory {
        return {
            createRelayStream: async () => {
                const multiChannelStream = await (source instanceof PromiseCompletionSource
                    ? (<PromiseCompletionSource<MultiChannelStream>>source).promise
                    : Promise.resolve(source));
                const sshChannelStream = await multiChannelStream.openStream(
                    channelType ?? TunnelRelayTunnelHost.clientStreamChannelType,
                );
                onClientChannelOpened?.(sshChannelStream);
                return new NodeStream(sshChannelStream);
            },
        };
    }

    public static throwing(error: Error): TunnelRelayStreamFactory {
        return {
            createRelayStream: () => {
                throw error;
            },
        };
    }
}
