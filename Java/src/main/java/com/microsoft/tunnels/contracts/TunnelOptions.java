package com.microsoft.tunnels.contracts;

import java.util.Collection;

public class TunnelOptions {
    /**
     * Specifies the set of connection protocol / implementations enabled for a
     * tunnel
     * or port. If unspecified, all supported modes are enabled.
     */
    Collection<TunnelConnectionMode> connectionModes;
}
