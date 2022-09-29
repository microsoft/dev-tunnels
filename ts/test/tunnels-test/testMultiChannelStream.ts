import { CancellationToken, MultiChannelStream, SshStream, Stream } from "@microsoft/dev-tunnels-ssh";

export class TestMultiChannelStream extends MultiChannelStream {
    constructor(
        public readonly serverStream: Stream, 
        public readonly clientStream: Stream,
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
        this.clientStream.dispose();
    }
}