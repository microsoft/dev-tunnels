// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// Generated from ../../../cs/src/Contracts/ProblemDetails.cs
/* eslint-disable */

/**
 * Structure of error details returned by the tunnel service, including validation errors.
 *
 * This object may be returned with a response status code of 400 (or other 4xx code). It
 * is compatible with RFC 7807 Problem Details (https://tools.ietf.org/html/rfc7807) and
 * https://docs.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.mvc.problemdetails but
 * doesn't require adding a dependency on that package.
 */
export interface ProblemDetails {
    /**
     * Gets or sets the error title.
     */
    title?: string;

    /**
     * Gets or sets the error detail.
     */
    detail?: string;

    /**
     * Gets or sets additional details about individual request properties.
     */
    errors?: { [property: string]: string[] };
}
