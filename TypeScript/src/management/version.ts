// Get the package version
import * as packageJson from './package.json';
const packageVersion = packageJson.version;

/**
 * Tunnel SDK user agent
 */
export const tunnelSdkUserAgent = `Visual-Studio-Tunnel-Service-SDK/${packageVersion}`;
