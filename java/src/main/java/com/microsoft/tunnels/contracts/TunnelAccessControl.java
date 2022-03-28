package com.microsoft.tunnels.contracts;

import java.util.Collection;

/**
 * Data contract for access control on a tunnel or port.
 */
public class TunnelAccessControl {
  /**
   * Gets or sets the list of access control entries.
   */
  public Collection<TunnelAccessControlEntry> entries;

  /**
   * Initializes a new instance of the {@link TunnelAccessControl} class
   * with a specified list of access control entries.
   */
  public TunnelAccessControl(Collection<TunnelAccessControlEntry> entries) {
    if (entries == null) {
      throw new IllegalArgumentException("Entries cannot be null.");
    }

    this.entries = entries;
  }
}
