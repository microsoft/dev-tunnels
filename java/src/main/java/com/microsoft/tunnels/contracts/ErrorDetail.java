// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// Generated from ../../../../../../../../cs/src/Contracts/ErrorDetail.cs

package com.microsoft.tunnels.contracts;

import com.google.gson.annotations.Expose;
import com.google.gson.annotations.SerializedName;

/**
 * The top-level error object whose code matches the x-ms-error-code response header
 */
public class ErrorDetail {
    /**
     * One of a server-defined set of error codes defined in {@link ErrorCodes}.
     */
    @Expose
    public String code;

    /**
     * A human-readable representation of the error.
     */
    @Expose
    public String message;

    /**
     * The target of the error.
     */
    @Expose
    public String target;

    /**
     * An array of details about specific errors that led to this reported error.
     */
    @Expose
    public ErrorDetail[] details;

    /**
     * An object containing more specific information than the current object about the
     * error.
     */
    @SerializedName("innererror")
    @Expose
    public InnerErrorDetail innerError;
}
