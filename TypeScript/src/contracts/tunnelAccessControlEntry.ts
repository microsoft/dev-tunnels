import { TunnelAccessControlEntryType } from './tunnelAccessControlEntryType';

/**
 * Data contract for an access control entry on a tunnel or tunnel port.
 */
export class TunnelAccessControlEntry {
    /**
     * Gets or sets the access control entry type.
     */
    public type: TunnelAccessControlEntryType;

    /**
     * Gets or sets a value indicating whether this is an access control entry on a tunnel
     * port that is inherited from the tunnel's access control list.
     */
    public isInherited?: boolean;

    /**
     * Gets or sets a value indicating whether this entry is a deny rule that blocks access
     * to the specified users. Otherwise it is an allow role.
     */
    public isDeny?: boolean;

    /**
     * Gets or sets the subjects for the entry, such as user or group IDs.
     * The format of the values depends on the type and provider of the entry.
     */
    public subjects: string[];

    /**
     * Gets or sets the access scopes that this entry grants or denies to the subjects.
     * These must be one or more values from TunnelAccessScopes.
     */
    public scopes: string[];

    /**
     * Gets or sets the provider of the subjects in this access control entry. The provider
     * impacts how the subject identifiers are resolved and displayed. The provider may be an
     * identity provider such as AAD, or a system or standard such as "ssh" or "ipv4".
     */
    public provider?: string;

    /**
     * Gets or sets an optional organization context for all subjects of this entry. The use
     * and meaning of this value depends on the Type and Provider of this entry.
     */
    public organization?: string;

    constructor() {
        this.type = TunnelAccessControlEntryType.None;
        this.scopes = [];
        this.subjects = [];
    }

    /**
     * @returns A compact textual representation of the access control entry.
     */
    public toString = (): string => {
        let res = '';
        if (this.isInherited) {
            res += 'Inherited: ';
        }

        res += this.isDeny ? '- ' : '+';
        res += this.getEntryTypeString(this.type, this.subjects?.length !== 1, this.provider);

        if (this.scopes && this.scopes.length > 0) {
            res += ` [${this.scopes.join(', ')}]`;
        }

        if (this.subjects && this.subjects.length > 0) {
            res += ` ([)${this.subjects.join(', ')})`;
        }

        return res;
    };

    private getEntryTypeString(
        entryType: TunnelAccessControlEntryType,
        plural: boolean,
        provider?: string,
    ): string {
        if (entryType === TunnelAccessControlEntryType.Anonymous) {
            plural = false;
        }

        let label;
        switch (entryType) {
            case TunnelAccessControlEntryType.Anonymous:
                label = 'Anonymous';
                break;
            case TunnelAccessControlEntryType.Users:
                label = 'User';
                break;
            case TunnelAccessControlEntryType.Groups:
                label = provider === Providers.github ? 'Team' : 'Group';
                break;
            case TunnelAccessControlEntryType.Organizations:
                label = provider === Providers.microsoft ? 'Tenant' : 'Org';
                break;
            case TunnelAccessControlEntryType.Repositories:
                label = 'Repo';
                break;
            case TunnelAccessControlEntryType.PublicKeys:
                label = 'Key';
                break;
            case TunnelAccessControlEntryType.IPAddressRanges:
                label = 'IP Range';
                break;
            default:
                label = entryType.toString();
                break;
        }

        if (plural) {
            label += 's';
        }

        if (provider) {
            switch (provider) {
                case Providers.microsoft:
                    label = `AAD ${label}`;
                    break;

                case Providers.github:
                    label = `GitHub ${label}`;
                    break;

                case Providers.ssh:
                    label = `SSH ${label}`;
                    break;

                case Providers.ipv4:
                    label = label.replace('IP', 'IPv4');
                    break;

                case Providers.ipv6:
                    label = label.replace('IP', 'IPv6');
                    break;

                default:
                    label = `${label} (${provider})`;
                    break;
            }
        }

        return label;
    }
}

/**
 * Constants for well-known identity providers.
 */
export class Providers {
    public static readonly microsoft: string = 'microsoft';
    public static readonly github: string = 'github';
    public static readonly ssh: string = 'ssh';
    public static readonly ipv4: string = 'ipv4';
    public static readonly ipv6: string = 'ipv6';
}
