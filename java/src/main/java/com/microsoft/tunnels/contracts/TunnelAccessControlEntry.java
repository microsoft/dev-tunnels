package com.microsoft.tunnels.contracts;

public class TunnelAccessControlEntry {
  /**
   * Gets or sets the access control entry type.
   */
  public TunnelAccessControlEntryType type;

  /**
   * Gets or sets a value indicating whether this is an access control entry on a
   * tunnel
   * port that is inherited from the tunnel's access control list.
   */
  public boolean isInherited;

  /**
   * Gets or sets a value indicating whether this entry is a deny rule that blocks
   * access to the specified users. Otherwise it is an allow rule.
   */
  public boolean isDeny;

  /**
   * Gets or sets a value indicating whether this entry applies to all subjects that are NOT
   * in the subjects list.
   */
  public boolean isInverse;

  /**
   * Gets or sets the subjects for the entry, such as user or group IDs.
   * The format of the values depends on the type and provider of the entry.
   */
  public String[] subjects;

  /**
   * Gets or sets the access scopes that this entry grants or denies to the
   * subjects.
   * These must be one or more values from TunnelAccessScopes.
   */
  public String[] scopes;

  /**
   * Gets or sets the provider of the subjects in this access control entry. The
   * provider
   * impacts how the subject identifiers are resolved and displayed. The provider
   * may be an
   * identity provider such as AAD, or a system or standard such as "ssh" or
   * "ipv4".
   */
  public String provider;

  /**
   * Gets or sets an optional organization context for all subjects of this entry.
   * The use
   * and meaning of this value depends on the Type and Provider of this entry.
   */
  public String organization;
}
