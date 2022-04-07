import { TunnelRelayTunnelHost } from '@vs/tunnels-connections';
import {
    Tunnel,
    TunnelAccessControlEntry,
    TunnelAccessControlEntryType,
    TunnelConnectionMode,
} from '@vs/tunnels-contracts';
import { TunnelManagementHttpClient, TunnelRequestOptions } from '@vs/tunnels-management';
import * as yargs from 'yargs';
import * as https from 'https';

const userAgent = { name: 'test-connection', version: '1.0' };

main()
    .then((exitCode) => process.exit(exitCode))
    .catch((e) => {
        console.error(e);
        process.exit(1);
    });

async function main() {
    const argv = await yargs.argv;

    let optionsArray = ((argv.o || argv.option) as string | string[]) || [];
    if (!Array.isArray(optionsArray)) {
        optionsArray = [optionsArray];
    }

    const options: { [name: string]: string } = {};
    for (let i = optionsArray.length - 1; i >= 0; i--) {
        const nameAndValue = optionsArray[i].split('=');
        if (nameAndValue.length === 2) {
            options[nameAndValue[0]] = nameAndValue[1];
        }
    }

    return startTunnelRelayHost();
}

async function startTunnelRelayHost() {
    let tunnelManagementClient = new TunnelManagementHttpClient(
        userAgent,
        () => Promise.resolve('Bearer'),
        'http://localhost:9900/', //'https://ci.dev.tunnels.vsengsaas.visualstudio.com/',
        new https.Agent({
            rejectUnauthorized: false,
        }),
    );
    let tunnelAccessControlEntry: TunnelAccessControlEntry = {
        type: TunnelAccessControlEntryType.Anonymous,
        subjects: [],
        scopes: ['connect'],
    };

    const tunnel: Tunnel = {
        clusterId: 'westus2',
        ports: [{ portNumber: 8000, protocol: 'auto' }],
        accessControl: {
            entries: [tunnelAccessControlEntry],
        }
    };
    let tunnelRequestOptions: TunnelRequestOptions = {
        tokenScopes: ['host'],
        includePorts: true,
    };
    let tunnelInstance = await tunnelManagementClient.createTunnel(tunnel, tunnelRequestOptions);
    let host = new TunnelRelayTunnelHost(tunnelManagementClient);
    host.trace = (level, eventId, msg, err) => {
        console.log(msg);
    };
    await host.start(tunnelInstance!);

    // Wait indefinitely so the connection does not close
    await new Promise(() => {});
    return 0;
}
