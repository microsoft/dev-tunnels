// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// Generated from ../../../../../../../../cs/src/Contracts/InnerErrorDetail.cs

package com.microsoft.tunnels.contracts;

import com.google.gson.annotations.Expose;
import com.google.gson.annotations.SerializedName;

/**
 * An object containing more specific information than the current object about the error.
 */
public class InnerErrorDetail {
    /**
     * A more specific error code than was provided by the containing error. One of a
     * server-defined set of error codes in {@link ErrorCodes}.
     */
    @Expose
    public String code;

    /**
     * An object containing more specific information than the current object about the
     * error.
     */
    @SerializedName("innererror")
    @Expose
    public InnerErrorDetail innerError;
}
