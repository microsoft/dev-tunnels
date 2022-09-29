// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

import { Queue, SshChannel, SshChannelClosedEventArgs, Stream } from '@microsoft/dev-tunnels-ssh';
import { Semaphore } from 'await-semaphore';

export class SshChannelForwarder {
    private readonly receiveQueue: Queue<Buffer>;
    private readonly receiveSemaphore: Semaphore;
    private channelClosedEvent?: SshChannelClosedEventArgs;

    public channel: SshChannel;
    public client: any;
    public stream: Stream;

    constructor(channel: SshChannel, tcpClient: any) {
        this.receiveQueue = new Queue<Buffer>();
        this.receiveSemaphore = new Semaphore(0);
        this.channel = channel;
        this.client = tcpClient;
        this.stream = tcpClient.getStream();

        this.init();
    }

    private async init() {
        await this.forwardFromStreamToChannel(new Buffer(4096));
        await this.forwardFromChannelToStream();
    }

    private onChannelDataReceived(sender: any, data: Buffer) {
        // Enqueue a copy of the buffer because the current one may be re-used by the caller.
        let copy = new Buffer(data.length);
        data.copy(copy);

        this.receiveQueue.enqueue(copy);

        try {
            this.receiveSemaphore.acquire();
        } catch (ex) {
            // The semaphore was disposed.
        }

        this.channel.adjustWindow(data.length);
    }

    private onChannelClosed(sender: any, e: SshChannelClosedEventArgs) {
        this.channelClosedEvent = e;

        try {
            this.receiveSemaphore.acquire();
        } catch (ex) {
            // The semaphore was disposed.
        }
    }

    private async forwardFromChannelToStream() {
        try {
            let forwarding;
            do {
                forwarding = await this.forwardFromChannelToStreamAsync();
            } while (forwarding);
        } catch (ex) {
            console.log(`Unexpected error reading channel stream: ${ex}`);
        }
    }

    private async forwardFromChannelToStreamAsync(): Promise<boolean> {
        try {
            await this.receiveSemaphore.acquire();
        } catch (ex) {
            // The semaphore was disposed.
            this.closeStream(true);
            return false;
        }

        let data = this.receiveQueue.dequeue();
        if (data) {
            try {
                await this.stream.write(data);
                return true;
            } catch (ex) {
                // The semaphore was disposed.
                this.closeStream(true);
                return false;
            }
        } else {
            if (this.channelClosedEvent != null) {
                let errorMessage =
                    this.channelClosedEvent.errorMessage ?? this.channelClosedEvent.error?.message;
                let message = !errorMessage
                    ? `Forwarder channel ${this.channel.channelId} closed.`
                    : `Forwarder channel ${this.channel.channelId} closed with error: ${errorMessage}`;
                console.log(message);

                this.closeStream(!!errorMessage);
            }

            // Reached end of stream.
            return false;
        }
    }

    private closeStream(abort: boolean) {
        try {
            if (abort) {
                let socket = this.client.Client;
                socket.close(); // Abort
            } else {
                this.stream.close();
            }
        } catch (ex) {
            console.log(`PortForwardingService unexpected error closing connection: ${ex}`);
            return;
        }

        console.log(`Channel forwarder ${abort ? 'aborted' : 'closed'} connection.`);
    }

    private async forwardFromStreamToChannel(buffer: Buffer) {
        try {
            let forwarding;
            do {
                forwarding = await this.forwardFromStreamToChannelAsync(buffer);
            } while (forwarding);
        } catch (ex) {
            console.log(`Unexpected error reading channel stream: ${ex}`);
        }
    }

    private async forwardFromStreamToChannelAsync(buffer: Buffer): Promise<boolean> {
        let count;
        let exception: any = null;
        try {
            const buf = await this.stream.read(buffer.byteLength);
            count = buf?.byteLength;
        } catch (ex) {
            exception = ex;
            count = 0;
        }

        if (count && count > 0) {
            await this.channel.send(buffer.slice(0, count));
            return true;
        } else if (!exception) {
            const message = 'Channel forwarder reached end of stream.';
            console.log(message);
            await this.channel.send(new Buffer(''));
            await this.channel.close();
        } else {
            const message = `Channel forwarder stream read error: ${exception}`;
            console.log(message);
            await this.channel.close('SIGABRT', exception.toString());
        }

        return false;
    }
}
