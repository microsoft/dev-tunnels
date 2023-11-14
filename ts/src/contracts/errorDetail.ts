// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// Generated from ../../../cs/src/Contracts/ErrorDetail.cs
/* eslint-disable */

import { InnerErrorDetail } from './innerErrorDetail';

/**
 * The top-level error object whose code matches the x-ms-error-code response header
 */
export interface ErrorDetail {
    /**
     * One of a server-defined set of error codes defined in {@link ErrorCodes}.
     */
    code: string;

    /**
     * A human-readable representation of the error.
     */
    message: string;

    /**
     * The target of the error.
     */
    target?: string;

    /**
     * An array of details about specific errors that led to this reported error.
     */
    details?: ErrorDetail[];

    /**
     * An object containing more specific information than the current object about the
     * error.
     */
    innererror?: InnerErrorDetail;
}
