// <copyright file="ErrorCodes.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.
// </copyright>

namespace Microsoft.DevTunnels.Contracts;

/// <summary>
/// Error codes for ErrorDetail.Code and `x-ms-error-code` header.
/// </summary>
public static class ErrorCodes
{
    /// <summary>
    /// Operation timed out.
    /// </summary>
    public const string Timeout = "Timeout";

    /// <summary>
    /// Operation cannot be performed because the service is not available.
    /// </summary>
    public const string ServiceUnavailable = "ServiceUnavailable";

    /// <summary>
    /// Internal error.
    /// </summary>
    public const string InternalError = "InternalError";
}
