// <copyright file="TunnelConnectionException.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.
// </copyright>

using System;

namespace Microsoft.VsSaaS.TunnelService
{
    /// <summary>
    /// Exception thrown when a host or client failed to connect to a tunnel.
    /// </summary>
    public class TunnelConnectionException : Exception
    {
        /// <summary>
        /// Creates a new instance of the <see cref="TunnelConnectionException" /> class
        /// with a message and optional inner exception.
        /// </summary>
        /// <param name="message">Exception message.</param>
        /// <param name="innerException">Optional inner exception.</param>
        public TunnelConnectionException(string message, Exception? innerException = null)
            : base(message, innerException)
        {
        }
    }
}
