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
        clientStreamFactory?: (stream: Stream) => Promise<{ stream: Stream, protocol: string }>,
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
        return Promise.resolve({ stream: this.stream, protocol: this.connectionType });
    };

    public static from(
        source: Stream | PromiseLike<Stream> | PromiseCompletionSource<Stream>,
        protocol: string,
    ): TunnelRelayStreamFactory {
        return {
            createRelayStream: async () => {
                return {
                    stream: await (source instanceof PromiseCompletionSource
                        ? (<PromiseCompletionSource<Stream>>source).promise
                        : Promise.resolve(source)),
                    protocol,
                };
            },
        };
    }

    public static fromMultiChannelStream(
        source:
            | MultiChannelStream
            | PromiseLike<MultiChannelStream>
            | PromiseCompletionSource<MultiChannelStream>,
        protocol: string,
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
                return {
                    stream: new NodeStream(sshChannelStream),
                    protocol,
                };
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
