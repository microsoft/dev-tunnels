// <copyright file="TraceSourceExtensions.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.
// </copyright>

using System.ComponentModel;
using System.Diagnostics;

namespace Microsoft.DevTunnels.Management
{
    /// <summary>
    /// Extension methods for tracing with a <see cref="TraceSource" />.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)] // Exclude from generated documentation
    public static class TraceSourceExtensions
    {
        /// <summary>
        /// Creates a new TraceSource with listeners and switch copied from the
        /// existing TraceSource.
        /// </summary>
        public static TraceSource WithName(this TraceSource trace, string name)
        {
            Requires.NotNull(trace, nameof(trace));

            var newTraceSource = new TraceSource(name);
            newTraceSource.Listeners.Clear(); // Remove the DefaultTraceListener
            newTraceSource.Listeners.AddRange(trace.Listeners);
            newTraceSource.Switch = trace.Switch;
            return newTraceSource;
        }

        /// <summary>Traces a critical message.</summary>
        [Conditional("TRACE")]
        public static void Critical(this TraceSource trace, string message)
        {
            trace.TraceEvent(TraceEventType.Critical, 0, message);
        }

        /// <summary>Traces a critical message with formatted arguments.</summary>
        [Conditional("TRACE")]
        public static void Critical(this TraceSource trace, string format, params object[] args)
        {
            trace.TraceEvent(TraceEventType.Critical, 0, format, args);
        }

        /// <summary>Traces an error message.</summary>
        [Conditional("TRACE")]
        public static void Error(this TraceSource trace, string message)
        {
            trace.TraceEvent(TraceEventType.Error, 0, message);
        }

        /// <summary>Traces an error message with formatted arguments.</summary>
        [Conditional("TRACE")]
        public static void Error(this TraceSource trace, string format, params object[] args)
        {
            trace.TraceEvent(TraceEventType.Error, 0, format, args);
        }

        /// <summary>Traces a warning message.</summary>
        [Conditional("TRACE")]
        public static void Warning(this TraceSource trace, string message)
        {
            trace.TraceEvent(TraceEventType.Warning, 0, message);
        }

        /// <summary>Traces a warning message with formatted arguments.</summary>
        [Conditional("TRACE")]
        public static void Warning(this TraceSource trace, string format, params object[] args)
        {
            trace.TraceEvent(TraceEventType.Warning, 0, format, args);
        }

        /// <summary>Traces an informational message.</summary>
        [Conditional("TRACE")]
        public static void Info(this TraceSource trace, string message)
        {
            trace.TraceEvent(TraceEventType.Information, 0, message);
        }

        /// <summary>Traces an informational message with formatted arguments.</summary>
        [Conditional("TRACE")]
        public static void Info(this TraceSource trace, string format, params object[] args)
        {
            trace.TraceEvent(TraceEventType.Information, 0, format, args);
        }

        /// <summary>Traces a verbose message.</summary>
        [Conditional("TRACE")]
        public static void Verbose(this TraceSource trace, string message)
        {
            trace.TraceEvent(TraceEventType.Verbose, 0, message);
        }

        /// <summary>Traces a verbose message with formatted arguments.</summary>
        [Conditional("TRACE")]
        public static void Verbose(this TraceSource trace, string format, params object[] args)
        {
            trace.TraceEvent(TraceEventType.Verbose, 0, format, args);
        }
    }
}
