using System;
using Microsoft.DevTunnels.Ssh;

namespace Microsoft.DevTunnels.Connections;

internal static class SshSessionExtensions
{
    public static string GetShortSessionId(this SshSession session)
    {
        if (session.SessionId == null || session.SessionId.Length < 16)
        {
            return string.Empty;
        }

        return new Guid(session.SessionId.AsSpan(0, 16)).ToString();
    }
}
