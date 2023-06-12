// <copyright file="SessionPortKey.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.
// </copyright>

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.DevTunnels.Connections;

/// <summary>
/// Class for comparing equality in SSH session ID port pairs.
/// </summary>
/// <remarks>
/// This class is public for testing purposes, and may be removed in the future.
/// </remarks>
public class SessionPortKey
{
    /// <summary>
    /// Session ID of the client SSH session, or null if the session does not have an ID
    /// (because it is not encrypted and not client-specific).
    /// </summary>
    public byte[]? SessionId { get; }

    /// <summary>
    /// Forwarded port number.
    /// </summary>
    public ushort Port { get; }

    /// <summary>
    /// Creates a new instance of the SessionPortKey class.
    /// </summary>
    public SessionPortKey(byte[]? sessionId, ushort port)
    {
        this.SessionId = sessionId;
        this.Port = port;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) =>
        obj is SessionPortKey other &&
        other.Port == this.Port &&
        ((this.SessionId == null && other.SessionId == null) ||
        ((this.SessionId != null && other.SessionId != null) &&
        Enumerable.SequenceEqual(other.SessionId, this.SessionId)));

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(
        this.Port,
        this.SessionId == null ? 0 :
            ((IStructuralEquatable)this.SessionId).GetHashCode(EqualityComparer<byte>.Default));
}
