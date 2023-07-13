// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

import { TunnelRelayTunnelClient } from '@microsoft/dev-tunnels-connections';
import { Tunnel, TunnelConnectionMode } from '@microsoft/dev-tunnels-contracts';
import { TunnelManagementHttpClient, TunnelRequestOptions } from '@microsoft/dev-tunnels-management';
import * as yargs from 'yargs';

const userAgent = 'test-connection/1.0';

main()
    .then((exitCode) => process.exit(exitCode))
    .catch((e) => {
        console.error(e);
        process.exit(1);
    });

async function main() {
    const argv = await yargs.argv;

    const port = ((argv.p || argv.port) as number) || 0;

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

    return startTunnelRelayConnection();
}

async function connect(port: number, options: { [name: string]: string }) {
    console.log('starting host....');

    const { execSync } = require('child_process');

    execSync(
        './starthost.ps1',
        { shell: 'powershell.exe', stdio: 'inherit' },
        (error: any, stdout: any, stderr: any) => {
            // output the messages
            console.log(error);
            console.log(stdout);
            console.log(stderr);
        },
    );

    return 0;
}

async function startTunnelRelayConnection() {
    let tunnelManagementClient = new TunnelManagementHttpClient(
        userAgent,
        () => Promise.resolve('Bearer'),
        'http://localhost:9900/');
    const tunnel: Tunnel = {
        tunnelId: '3xfp2wn8',
        clusterId: 'westus2'
    };
    let tunnelRequestOptions: TunnelRequestOptions = {
        tokenScopes: ['connect'],
        accessToken: '',
    };

    let tunnelInstance = await tunnelManagementClient.getTunnel(tunnel, tunnelRequestOptions);

    let tunnelRelayTunnelClient = new TunnelRelayTunnelClient();
    await tunnelRelayTunnelClient.connect(tunnelInstance!);
    // Wait indefinitely so the connection does not close
    await new Promise((resolve) => {});
    return 0;
}
