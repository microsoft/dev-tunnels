package com.microsoft.tunnels.contracts;

/**
 * Defines possible values for the protocol of a TunnelPort.
 */
public class TunnelProtocol {
  /**
   * The protocol is automatically detected. (TODO: Define detection semantics.)
   */
  public static String Auto = "auto";

  /**
   * Unknown TCP protocol.
   */
  public static String Tcp = "tcp";

  /**
   * Unknown UDP protocol.
   */
  public static String Udp = "udp";

  /**
   * SSH protocol.
   */
  public static String Ssh = "ssh";

  /**
   * Remote desktop protocol.
   */
  public static String Rdp = "rdp";

  /**
   * HTTP protocol.
   */
  public static String Http = "http";

  /**
   * HTTPS protocol.
   */
  public static String Https = "https";
}
