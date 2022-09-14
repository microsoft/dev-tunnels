// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// Generated from ../../../../../../../../cs/src/Contracts/TunnelSshKeyResponse.cs

package com.microsoft.tunnels.contracts;

import com.google.gson.annotations.Expose;

/**
 * Response for SshKey endpoint.
 */
public class TunnelSshKeyResponse {
    /**
     * Gets or sets the ssh key for a tunnel.
     */
    @Expose
    public String sshKey;
}
