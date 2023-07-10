// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

// Get the package version
import * as packageJson from './package.json';
const packageVersion = packageJson.version;

/**
 * Tunnel SDK user agent
 */
export const tunnelSdkUserAgent = `Dev-Tunnels-Service-TypeScript-SDK/${packageVersion}`;
