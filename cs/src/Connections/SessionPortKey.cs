// <copyright file="SessionPortKey.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.
// </copyright>

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.DevTunnels.Connections
{
    /// <summary>
    /// Class for comparing equality in sessionId port pairs
    /// </summary>
    public class SessionPortKey
    {
        /// <summary>
        /// Session Id from host
        /// </summary>
        public byte[] SessionId { get; }

        /// <summary>
        /// Port that is hosted client side
        /// </summary>
        public ushort Port { get; }

        /// <summary>
        /// Creates a new instance of the SessionPortKey class.
        /// </summary>
        public SessionPortKey(byte[] sessionId, ushort port)
        {
            if (sessionId == null)
            {
                throw new ArgumentNullException("SessionId cannot be null");
            }

            this.SessionId = sessionId;
            this.Port = port;
        }

        /// <inheritdoc />
        public override bool Equals(object? obj) =>
            obj is SessionPortKey other &&
            other.Port == this.Port &&
            Enumerable.SequenceEqual(other.SessionId, this.SessionId);

        /// <inheritdoc />
        public override int GetHashCode() =>
            HashCode.Combine(
                this.Port,
                ((IStructuralEquatable)this.SessionId).GetHashCode(EqualityComparer<byte>.Default));
    }
}
