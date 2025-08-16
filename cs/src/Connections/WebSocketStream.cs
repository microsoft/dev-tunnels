// <copyright file="WebSocketStream.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.
// </copyright>

using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.DevTunnels.Connections
{
    /// <summary>
    /// Wraps a <see cref="WebSocket"/> to adapt it to <see cref="Stream"/>.
    /// </summary>
    /// <remarks>
    /// Cancelling reads or writes will abort the web socket connection.
    /// If backs SshSession when it is closed, SshSession cancels the read from the stream, which will abort the web socket.
    /// </remarks>
    public class WebSocketStream : Stream
    {
        /// <summary>
        /// Maximum length of close description. Everything longer will be truncted.
        /// </summary>
        public const int CloseDescriptionMaxLength = 123;

        private const int LastWriteTimeoutBeforeCloseMs = 15_000;
        private const int CloseTimeoutMs = 15_000;

        private readonly object lockObject = new object();
        private readonly CancellationTokenSource writeCts = new CancellationTokenSource();
        private readonly TaskCompletionSource<object?> disposeCompletion = new TaskCompletionSource<object?>();

        private static readonly bool ShouldTraceWebSocketMessages = String.Equals(
            Environment.GetEnvironmentVariable("DEVTUNNELS_LOG_WEBSOCKET_MESSAGES"),
            "true",
            StringComparison.OrdinalIgnoreCase);

        // Thread-safety:
        // - It's acceptable to call ReceiveAsync and SendAsync in parallel.  One of each may run concurrently.
        // - It's acceptable to have a pending ReceiveAsync while CloseOutputAsync or CloseAsync is called.
        // - Attempting to invoke any other operations in parallel may corrupt the instance.  Attempting to invoke
        //   a send operation while another is in progress or a receive operation while another is in progress will
        //   result in an exception.
        private readonly WebSocket socket;

        private WebSocketCloseStatus? closeStatus;
        private string? closeStatusDescription;
        private Task? lastWriteTask;
        private bool isDisposed;

        private TraceSource Trace;

        /// <summary>
        /// Creates a new instance of <see cref="WebSocket"/> wrapping connected <paramref name="socket"/>.
        /// If the <paramref name="socket"/> is not in <see cref="WebSocketState.Open"/> it will be closed.
        /// </summary>
        /// <param name="socket">Web socket to wrap.</param>
        /// <param name="trace">Trace source for logging.</param>
        /// <exception cref="ArgumentNullException">If <paramref name="socket"/> is null.</exception>
        /// <exception cref="ArgumentException">If <paramref name="socket"/> has not connected yet (i.e in <see cref="WebSocketState.Connecting"/> state).</exception>
        public WebSocketStream(WebSocket socket, TraceSource? trace)
        {
            this.socket = Requires.NotNull(socket, nameof(socket));
            Requires.Argument(socket.State != WebSocketState.Connecting, nameof(socket), "The web socket has not connected yet.");
            if (socket.State != WebSocketState.Open)
            {
                Close();
            }
            Trace = trace ?? new TraceSource(nameof(WebSocketStream), SourceLevels.Verbose);
        }

        /// <summary>
        /// Gets the websocket sub-protocol.
        /// </summary>
        public string SubProtocol => this.socket.SubProtocol!;

        /// <summary>
        /// Current web socket close status or null if not closed.
        /// </summary>
        public WebSocketCloseStatus? CloseStatus
        {
            get => this.socket.CloseStatus;
            set => this.closeStatus = value;
        }

        /// <summary>
        /// Current web socket close status description..
        /// </summary>
        public string? CloseStatusDescription
        {
            get => this.socket.CloseStatusDescription;
            set => this.closeStatusDescription = value;
        }

        /// <summary>
        /// Gets the HTTP request ID from the web socket connection, if available.
        /// </summary>
        /// <remarks>
        /// The request ID is returned as a response HTTP header when the websocket connection
        /// is established. The value can then be added to client-reported events to support
        /// correlation with service events.
        /// </remarks>
        public string? RequestId
        {
            get
            {
#if NET8_0_OR_GREATER
                var responseHeaders = (this.socket as ClientWebSocket)?.HttpResponseHeaders;
                if (responseHeaders?.TryGetValue("VsSaaS-Request-ID", out var requestIdValues) == true)
                {
                    var requestId = requestIdValues.FirstOrDefault();
                    if (!string.IsNullOrEmpty(requestId))
                    {
                        return requestId;
                    }
                }
#endif

                return null;
            }
        }

        /// <summary>
        /// Connect to web socket.
        /// </summary>
        public static async Task<WebSocketStream> ConnectToWebSocketAsync(Uri uri, Action<ClientWebSocketOptions>? configure = default, TraceSource? trace = default, CancellationToken cancellation = default)
        {
            var socket = new ClientWebSocket();

#if NET8_0_OR_GREATER
            // Enable access to HTTP response headers.
            socket.Options.CollectHttpResponseDetails = true;
#endif

            try
            {
                configure?.Invoke(socket.Options);
                await socket.ConnectAsync(uri, cancellation);
                return new WebSocketStream(socket, trace);
            }
            catch (WebSocketException wse) when (wse.WebSocketErrorCode == WebSocketError.NotAWebSocket)
            {
                // The http request didn't upgrade to a web socket and instead may have returned an error status code.
#if NET8_0_OR_GREATER
                if ((int)socket.HttpStatusCode >= 400)
                {
                    TunnelConnectionException.SetHttpStatusCode(wse, socket.HttpStatusCode);
                }
#else
                // Socket.HttpStatusCode is not available in older versions of .NET.
                // As a workaround, check for "'{actual response code}'" pattern in the exception message,
                // which may look like this: "The server returned status code '403' when status code '101' was expected".
                int i = wse.Message.IndexOf('\'');
                if (i >= 0)
                {
                    int j = wse.Message.IndexOf('\'', i + 1);
                    if (j > i + 1 &&
                        int.TryParse(
                            wse.Message.Substring(i + 1, j - i - 1),
                            NumberStyles.None,
                            CultureInfo.InvariantCulture,
                            out var statusCode) &&
                        statusCode != 101)
                    {
                        TunnelConnectionException.SetHttpStatusCode(wse, (HttpStatusCode)statusCode);
                    }
                }
#endif

                socket.Dispose();
                throw;
            }
            catch
            {
                socket.Dispose();
                throw;
            }
        }

        /// <summary>
        /// Close stream and the web socket with <paramref name="closeStatus"/> and <paramref name="closeStatusDescription"/>.
        /// </summary>
        /// <remarks>
        /// If the socket is already closed, this is no-op, <see cref="CloseStatus"/> and <see cref="CloseStatusDescription"/> do not change.
        /// </remarks>
        public ValueTask CloseAsync(WebSocketCloseStatus closeStatus, string? closeStatusDescription = default)
        {
            this.closeStatus = closeStatus;
            this.closeStatusDescription = closeStatusDescription;
            return DisposeAsync();
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            if (disposing && !this.isDisposed)
            {
                DisposeAsync().AsTask().Wait();
            }

            base.Dispose(disposing);
        }

        /// <inheritdoc/>
        public override async ValueTask DisposeAsync()
        {
            Trace.TraceEvent(TraceEventType.Verbose, 0, "WebSocketStream.DisposeAsync: Disposing");

            Task? lastWriteTask = null;
            bool disposing = false;
            if (!this.isDisposed)
            {
                lock (this.lockObject)
                {
                    disposing = !this.isDisposed;
                    this.isDisposed = true;
                    lastWriteTask = this.lastWriteTask;
                }
            }

            if (!disposing)
            {
                await this.disposeCompletion.Task;
                return;
            }

            // We cannot write in parallel with closing the socket, so we need to wait for the last write task.
            // We will give it some timeout if there is no debugger attached.
            if (lastWriteTask != null)
            {
                if (!Debugger.IsAttached)
                {
                    this.writeCts.CancelAfter(LastWriteTimeoutBeforeCloseMs);
                }

                try
                {
                    await lastWriteTask.ConfigureAwait(false);
                }
                catch
                {
                    // Ignore exceptions here. The caller of the last write will observer them.
                }
            }

            this.writeCts.Dispose();

            if (this.socket.State != WebSocketState.Closed && this.socket.State != WebSocketState.Aborted)
            {
                using CancellationTokenSource cts = new CancellationTokenSource();
                try
                {
                    // The socket must be in one of the following states now. It's OK to call CloseAsync on it.
                    // WebSocketState.Open, WebSocketState.CloseReceived, WebSocketState.CloseSent

                    var closeStatus = this.closeStatus ?? WebSocketCloseStatus.NormalClosure;
                    string? closeStatusDescription =
                        this.closeStatusDescription == null ? null :
                        this.closeStatusDescription.Length <= CloseDescriptionMaxLength ? this.closeStatusDescription :
                        this.closeStatusDescription.Substring(0, CloseDescriptionMaxLength);

                    if (!Debugger.IsAttached)
                    {
                        cts.CancelAfter(CloseTimeoutMs);
                    }

                    await this.socket.CloseAsync(closeStatus, closeStatusDescription, cts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    TraceEvent(TraceEventType.Warning, 0, "WebSocketStream.DisposeAsync: Timed out waiting for close handshake");
                }
                catch (ObjectDisposedException)
                {
                    TraceEvent(TraceEventType.Warning, 0, "WebSocketStream.DisposeAsync: Called on disposed socket");
                }
                catch (WebSocketException wse)
                when (wse.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely)
                {
                    // Other party closed connection prematurely.
                    TraceEvent(TraceEventType.Warning, 0, "WebSocketStream.DisposeAsync: Socket closed prematurely exception: {0}", wse);
                }
                catch (Exception ex)
                {
                    // Do not throw from DisposeAsync.
                    TraceEvent(TraceEventType.Warning, 0, "WebSocketStream.DisposeAsync: Exception during DisposeAsync: {0}", ex);
                }
            }

            this.socket.Dispose();
            await base.DisposeAsync();
            this.disposeCompletion.TrySetResult(null);
        }

        /// <inheritdoc/>
        public override bool CanRead => !this.isDisposed;

        /// <inheritdoc/>
        public override bool CanSeek => false;

        /// <inheritdoc/>
        public override bool CanWrite => !this.isDisposed;

        /// <inheritdoc/>
        public override bool CanTimeout => false;

        /// <inheritdoc/>
        public override long Length =>
            throw new InvalidOperationException();

        /// <inheritdoc/>
        public override long Position
        {
            get => throw new InvalidOperationException();
            set => throw new InvalidOperationException();
        }

        /// <inheritdoc/>
        public override void Flush()
        {
            ThrowIfDisposed();
        }

        /// <inheritdoc/>
        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public override int Read(byte[] buffer, int offset, int count) =>
            ReadAsync(buffer, offset, count).GetAwaiter().GetResult();

        /// <inheritdoc/>
        public override long Seek(long offset, SeekOrigin origin) =>
            throw new InvalidOperationException();

        /// <inheritdoc/>
        public override void SetLength(long value) =>
            throw new InvalidOperationException();

        /// <inheritdoc/>
        public override void Write(byte[] buffer, int offset, int count) =>
            WriteAsync(buffer, offset, count).GetAwaiter().GetResult();

        /// <inheritdoc/>
        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            Requires.NotNull(buffer, nameof(buffer));
            Requires.Range(offset >= 0 && offset < buffer.Length, nameof(offset));
            Requires.Range(count >= 0 && count <= buffer.Length - offset, nameof(count));

            if (!CanReadFromSocket)
            {
                return 0;
            }

            try
            {
                Task<WebSocketReceiveResult> task;
                lock (this.lockObject)
                {
                    if (!CanReadFromSocket)
                    {
                        return 0;
                    }

                    task = this.socket.ReceiveAsync(new ArraySegment<byte>(buffer, offset, count), cancellationToken);
                }

                var result = await task.ConfigureAwait(false);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    // Other party is closing the socket.
                    Close();
                    Trace.TraceEvent(TraceEventType.Verbose, 0, "WS.R: Connection close message");
                    return 0;
                }
                TraceEvent(TraceEventType.Verbose, 0, "WS.R: Received message {0}", result.Count);

                return result.Count;
            }
            catch (OperationCanceledException)
            {
                // Cancellation requested or socket was closed.
                // If the socket was closed, treat this as the end of the stream.
                if (!CanReadFromSocket)
                {
                    Trace.TraceEvent(TraceEventType.Verbose, 0, "WS.R: Socket closed");
                    return 0;
                }
                TraceEvent(TraceEventType.Warning, 0, "WS.R: Operation cancelled exception");

                throw;
            }
            catch (WebSocketException wse)
            when (wse.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely)
            {
                // Other party closed connection prematurely.
                Close();
                TraceEvent(TraceEventType.Warning, 0, "WS.R: Socket closed prematurely exception: {0}, SocketState={1}, CloseStatus={2}, CloseStatusDescription={3}", wse, socket.State, socket.CloseStatus, socket.CloseStatusDescription);
                return 0;
            }
        }

        /// <inheritdoc/>
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (!CanReadFromSocket)
            {
                return 0;
            }

            try
            {
                ValueTask<ValueWebSocketReceiveResult> task;
                lock (this.lockObject)
                {
                    if (!CanReadFromSocket)
                    {
                        return 0;
                    }

                    task = this.socket.ReceiveAsync(buffer, cancellationToken);
                }

                var result = await task.ConfigureAwait(false);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    // Other party is closing the socket.
                    Close();
                    TraceEvent(TraceEventType.Verbose, 0, "WS.R: Connection close message");
                    return 0;
                }
                TraceEvent(TraceEventType.Verbose, 0, "WS.R: Received message {0}", result.Count);

                return result.Count;
            }
            catch (OperationCanceledException)
            {
                // Cancellation requested or socket was closed.
                // If the socket was closed, treat this as the end of the stream.
                if (!CanReadFromSocket)
                {
                    TraceEvent(TraceEventType.Verbose, 0, "WS.R: Socket closed");
                    return 0;
                }
                TraceEvent(TraceEventType.Warning, 0, "WS.R: Operation cancelled exception");

                throw;
            }
            catch (WebSocketException wse)
            when (wse.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely)
            {
                // Other party closed connection prematurely.
                Close();
                Trace.TraceEvent(TraceEventType.Warning, 0, "WS.R: Socket closed prematurely exception: {0}, SocketState={1}, CloseStatus={2}, CloseStatusDescription={3}", wse, socket.State, socket.CloseStatus, socket.CloseStatusDescription);
                return 0;
            }
        }

        /// <inheritdoc/>
        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            Requires.NotNull(buffer, nameof(buffer));
            Requires.Range(offset >= 0 && offset < buffer.Length, nameof(offset));
            Requires.Range(count >= 0 && count <= buffer.Length - offset, nameof(count));

            ThrowIfCannotWrite();

            var cts = cancellationToken.CanBeCanceled ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, this.writeCts.Token) : default;
            Task? writeTask = null;
            try
            {
                lock (this.lockObject)
                {
                    ThrowIfCannotWrite();

                    writeTask = this.socket.SendAsync(
                        new ArraySegment<byte>(buffer, offset, count),
                        WebSocketMessageType.Binary,
                        true /* end of message */,
                        cancellationToken.CanBeCanceled ? cts!.Token : this.writeCts.Token);

                    this.lastWriteTask = writeTask;
                }

                await writeTask.ConfigureAwait(false);
                TraceEvent(TraceEventType.Verbose, 0, "WS.W: Wrote message {0}", count);
            }
            catch (OperationCanceledException oce)
            when (this.writeCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                TraceEvent(TraceEventType.Warning, 0, "WS.W: Operation cancelled exception");
                // Other thread is trying to dispose and is cancelling this write.
                // Report that the object is disposed to the caller.
                throw new ObjectDisposedException(GetType().Name, oce);
            }
            catch (WebSocketException wse)
            {
                Close();
                if (wse.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely)
                {
                    TraceEvent(TraceEventType.Warning, 0, "WS.W: Websocket closed prematurely exception");
                    // Socket closed prematurely, treat as if the connection closed.
                    throw new ObjectDisposedException(GetType().Name, wse);
                }

                TraceEvent(TraceEventType.Warning, 0, "WS.W: Websocket exception {0}", wse.Message);
                throw;
            }
            finally
            {
                if (writeTask != null)
                {
                    lock (this.lockObject)
                    {
                        if (this.lastWriteTask == writeTask)
                        {
                            this.lastWriteTask = null;
                        }
                    }
                }

                cts?.Dispose();
            }
        }

        /// <inheritdoc/>
        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            ThrowIfCannotWrite();

            var cts = cancellationToken.CanBeCanceled ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, this.writeCts.Token) : default;
            Task? writeTask = null;
            try
            {
                lock (this.lockObject)
                {
                    ThrowIfCannotWrite();

                    writeTask = this.socket.SendAsync(
                        buffer,
                        WebSocketMessageType.Binary,
                        true /* end of message */,
                        cancellationToken.CanBeCanceled ? cts!.Token : this.writeCts.Token).AsTask();

                    this.lastWriteTask = writeTask;
                }

                await writeTask.ConfigureAwait(false);
                TraceEvent(TraceEventType.Verbose, 0, "WS.W: Wrote message");

                lock (this.lockObject)
                {
                    if (this.lastWriteTask == writeTask)
                    {
                        this.lastWriteTask = null;
                    }
                }
            }
            catch (OperationCanceledException oce)
            when (this.writeCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                TraceEvent(TraceEventType.Warning, 0, "WS.W: Operation cancelled exception");

                // Other thread is trying to dispose and is cancelling this write.
                // Report that the object is disposed to the caller.
                throw new ObjectDisposedException(GetType().Name, oce);
            }
            catch (WebSocketException wse)
            {
                Close();
                if (wse.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely)
                {
                    Trace.TraceEvent(TraceEventType.Warning, 0, "WS.W: Websocket closed prematurely exception");
                    // Socket closed prematurely, treat as if the connection closed.
                    throw new ObjectDisposedException(GetType().Name, wse);
                }

                TraceEvent(TraceEventType.Warning, 0, "WS.W: Websocket exception {0}", wse.Message);
                throw;
            }
            finally
            {
                if (writeTask != null)
                {
                    lock (this.lockObject)
                    {
                        if (this.lastWriteTask == writeTask)
                        {
                            this.lastWriteTask = null;
                        }
                    }
                }

                cts?.Dispose();
            }
        }

        private void TraceEvent(TraceEventType eventType, int id, string? format, params object?[]? args)
        {
            if (ShouldTraceWebSocketMessages)
            {
                Trace.TraceEvent(eventType, id, format, args);
            }
        }

        private bool CanReadFromSocket =>
            (this.socket.State == WebSocketState.Open || this.socket.State == WebSocketState.CloseSent) &&
            !this.isDisposed;

        private void ThrowIfCannotWrite()
        {
            ThrowIfDisposed();
            if (this.socket.State != WebSocketState.Open && this.socket.State != WebSocketState.CloseReceived)
            {
                throw new ObjectDisposedException(GetType().Name);
            }
        }

        private void ThrowIfDisposed()
        {
            if (this.isDisposed)
            {
                throw new ObjectDisposedException(GetType().Name);
            }
        }
    }
}
