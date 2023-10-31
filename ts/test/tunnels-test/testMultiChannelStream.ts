import { CancellationToken, MultiChannelStream, SshDisconnectReason, SshStream, Stream } from "@microsoft/dev-tunnels-ssh";
import { waitForEvent } from "./promiseUtils";

export class TestMultiChannelStream extends MultiChannelStream {
    constructor(
        public readonly serverStream: Stream, 
    ) {
        super(serverStream);
    }

    public streamsOpened = 0;

    public async openStream(channelType?: string, cancellation?: CancellationToken): Promise<SshStream> {
        const result = super.openStream(channelType, cancellation);
        this.streamsOpened++;
        return result;
    }

    public dropConnection() {
        this.serverStream.dispose();
    }

	public async close(reason?: SshDisconnectReason, message?: string) {
		if (reason !== undefined) {
            await this.session.close(reason, message);
		}

        await super.close();
	}

    public async waitUntilClosed() : Promise<void> {
        if (!this.isClosed) {
            await waitForEvent(this.onClosed);
        }
    }
}