import { TunnelAccessControlEntry } from './tunnelAccessControlEntry';

/**
 * Data contract for access control on a tunnel or port.
 */
export interface TunnelAccessControl {
    /**
     * Gets or sets the list of access control entries.
     */
    entries: TunnelAccessControlEntry[];
}
