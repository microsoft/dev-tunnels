package com.microsoft.tunnels.contracts;

import com.google.gson.annotations.SerializedName;

public enum TunnelAccessControlEntryType {
  /**
   * Uninitialized access control entry type.
   */
  @SerializedName("none")
  None,

  /**
   * The access control entry refers to all anonymous users.
   */
  @SerializedName("anonymous")
  Anonymous,

  /**
   * The access control entry is a list of user IDs that are allowed (or denied)
   * access.
   */
  @SerializedName("users")
  Users,

  /**
   * The access control entry is a list of groups IDs that are allowed (or denied)
   * access.
   */
  @SerializedName("groups")
  Groups,

  /**
   * The access control entry is a list of organization IDs that are allowed (or
   * denied) access.
   */
  @SerializedName("organizations")
  Organizations,

  /**
   * The access control entry is a list of repositories.
   * Users are allowed access to the tunnel if they have access to the repo.
   */
  @SerializedName("repositories")
  Repositories,

  /**
   * The access control entry is a list of public keys.
   * Users are allowed access if they can authenticate using a private key
   * corresponding to one of the public keys.
   */
  @SerializedName("publickeys")
  PublicKeys,

  /**
   * The access control entry is a list of IP address ranges that are allowed (or
   * denied) access to the tunnel.
   */
  @SerializedName("ipaddressranges")
  IPAddressRanges,
}
