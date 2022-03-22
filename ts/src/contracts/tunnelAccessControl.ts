// Generated from ../../../cs/src/Contracts/TunnelAccessControl.cs

import { TunnelAccessControlEntry } from './tunnelAccessControlEntry';

/**
 * Data contract for access control on a {@link Tunnel} or {@link TunnelPort}.
 *
 * Tunnels and tunnel ports can each optionally have an access-control property set on
 * them. An access-control object contains a list (ACL) of entries (ACEs) that specify the
 * access scopes granted or denied to some subjects. Tunnel ports inherit the ACL from the
 * tunnel, though ports may include ACEs that augment or override the inherited rules. 
 * Currently there is no capability to define "roles" for tunnel access (where a role
 * specifies a set of related access scopes), and assign roles to users. That feature may
 * be added in the future. (It should be represented as a separate `RoleAssignments`
 * property on this class.)
 */
export interface TunnelAccessControl {
    /**
     * Gets or sets the list of access control entries.
     *
     * The order of entries is significant: later entries override earlier entries that
     * apply to the same subject. However, deny rules are always processed after allow
     * rules, therefore an allow rule cannot override a deny rule for the same subject.
     */
    entries: TunnelAccessControlEntry[];
}

// Import static members from a non-generated file,
// and re-export them as an object with the same name as the interface.
import {
    validateScopes,
} from './tunnelAccessControlStatics';

export const TunnelAccessControl = {
    validateScopes,
};
