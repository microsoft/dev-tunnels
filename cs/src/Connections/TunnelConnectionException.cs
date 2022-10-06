// <copyright file="TunnelConnectionException.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.
// </copyright>

using System;
using System.Net;

namespace Microsoft.DevTunnels.Connections;

/// <summary>
/// Exception thrown when a host or client failed to connect to a tunnel.
/// </summary>
public class TunnelConnectionException : Exception
{
    private const string HttpStatusCodeKey = "HttpStatusCode";

    /// <summary>
    /// Creates a new instance of the <see cref="TunnelConnectionException" /> class
    /// with a message and optional inner exception.
    /// </summary>
    /// <param name="message">Exception message.</param>
    /// <param name="innerException">Optional inner exception.</param>
    public TunnelConnectionException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
        if (innerException != null)
        {
            var statusCode = GetHttpStatusCode(innerException);
            if (statusCode != default)
            {
                StatusCode = statusCode;
            }
        }
    }

    /// <summary>
    /// Creates a new instance of the <see cref="TunnelConnectionException" /> class
    /// with a message, HTTP status code, and optional inner exception.
    /// </summary>
    /// <param name="message">Exception message.</param>
    /// <param name="statusCode">HTTP status code.</param>
    /// <param name="innerException">Optional inner exception.</param>
    public TunnelConnectionException(
        string message,
        HttpStatusCode statusCode,
        Exception? innerException = null)
        : this(message, innerException)
    {
        StatusCode = statusCode;
    }

    /// <summary>
    /// Gets the HTTP status code from the exception, or null if no status code is available.
    /// </summary>
    public HttpStatusCode? StatusCode { get; private set; }

    internal static HttpStatusCode GetHttpStatusCode(Exception ex)
    {
        var value = ex.Data[HttpStatusCodeKey];
        return value is HttpStatusCode ? (HttpStatusCode)value : default;
    }

    internal static void SetHttpStatusCode(Exception ex, HttpStatusCode statusCode)
    {
        ex.Data[HttpStatusCodeKey] = statusCode;
    }
}