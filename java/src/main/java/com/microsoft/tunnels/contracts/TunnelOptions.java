package com.microsoft.tunnels.contracts;

import com.google.gson.annotations.Expose;

import java.util.Collection;

/**
 * TunnelOptions.
 */
public class TunnelOptions {
  /**
   * Specifies the set of connection protocol / implementations enabled for a
   * tunnel
   * or port. If unspecified, all supported modes are enabled.
   */
  @Expose
  public Collection<TunnelConnectionMode> connectionModes;
}
