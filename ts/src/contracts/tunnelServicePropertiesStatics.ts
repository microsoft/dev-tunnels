// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

import {
    TunnelServiceProperties as ITunnelServiceProperties,
    prodFirstPartyAppId,
    ppeFirstPartyAppId,
    devServiceAppId,
    prodThirdPartyAppId,
    ppeThirdPartyAppId,
    devThirdPartyAppId,
    prodGitHubAppClientId,
    ppeGitHubAppClientId,
    devGitHubAppClientId,
    localGitHubAppClientId,
    prodDnsName,
    ppeDnsName,
    devDnsName,
    localDnsName,
} from './tunnelServiceProperties';

/**
 * Gets production service properties.
 */
export const production = <ITunnelServiceProperties>{
    serviceUri: `https://${prodDnsName}/`,
    serviceAppId: prodFirstPartyAppId,
    serviceInternalAppId: prodThirdPartyAppId,
    gitHubAppClientId: prodGitHubAppClientId,
};

/**
 * Gets properties for the service in the staging environment (PPE).
 */
export const staging = <ITunnelServiceProperties>{
    serviceUri: `https://${ppeDnsName}/`,
    serviceAppId: ppeFirstPartyAppId,
    serviceInternalAppId: ppeThirdPartyAppId,
    gitHubAppClientId: ppeGitHubAppClientId,
};

/**
 * Gets properties for the service in the development environment.
 */
export const development = <ITunnelServiceProperties>{
    serviceUri: `https://${devDnsName}/`,
    serviceAppId: devServiceAppId,
    serviceInternalAppId: devThirdPartyAppId,
    gitHubAppClientId: devGitHubAppClientId,
};

/**
 * Gets properties for the service when running locally.
 *
 * Uses the same service app IDs as the development environment, but a different
 * GitHub app with localhost callback URLs.
 */
export const local = <ITunnelServiceProperties>{
    serviceUri: `https://${localDnsName}/`,
    serviceAppId: devServiceAppId,
    serviceInternalAppId: devThirdPartyAppId,
    gitHubAppClientId: localGitHubAppClientId,
};

/**
 * Gets properties for the service in the specified environment.
 */
export function environment(environmentName: string): ITunnelServiceProperties {
    if (!environmentName) {
        throw new Error(`Invalid argument: ${environmentName}`);
    }

    switch (environmentName.toLowerCase()) {
        case 'prod':
        case 'production':
            return production;
        case 'ppe':
        case 'preprod':
            return staging;
        case 'dev':
        case 'development':
            return development;
        case 'local':
            return local;
        default:
            throw new Error(`Invalid service environment: ${environmentName}`);
    }
}
