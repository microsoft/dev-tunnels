// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// Generated from ../../../cs/src/Contracts/InnerErrorDetail.cs
/* eslint-disable */

/**
 * An object containing more specific information than the current object about the error.
 */
export interface InnerErrorDetail {
    /**
     * A more specific error code than was provided by the containing error. One of a
     * server-defined set of error codes in {@link ErrorCodes}.
     */
    code: string;

    /**
     * An object containing more specific information than the current object about the
     * error.
     */
    innererror?: InnerErrorDetail;
}
