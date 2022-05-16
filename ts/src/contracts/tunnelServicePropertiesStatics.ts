// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

import {
    TunnelServiceProperties as ITunnelServiceProperties,
    prodFirstPartyAppId,
    nonProdFirstPartyAppId,
    prodThirdPartyAppId,
    ppeThirdPartyAppId,
    devThirdPartyAppId,
    prodGitHubAppClientId,
    nonProdGitHubAppClientId,
    prodDnsName,
    ppeDnsName,
    devDnsName,
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
    serviceAppId: nonProdFirstPartyAppId,
    serviceInternalAppId: ppeThirdPartyAppId,
    gitHubAppClientId: nonProdGitHubAppClientId,
};

/**
 * Gets properties for the service in the development environment.
 */
export const development = <ITunnelServiceProperties>{
    serviceUri: `https://${devDnsName}/`,
    serviceAppId: nonProdFirstPartyAppId,
    serviceInternalAppId: devThirdPartyAppId,
    gitHubAppClientId: nonProdGitHubAppClientId,
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
        default:
            throw new Error(`Invalid service environment: ${environmentName}`);
    }
}
