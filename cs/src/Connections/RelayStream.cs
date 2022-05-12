// <copyright file="RelayStream.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.
// </copyright>

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Relay;

namespace Microsoft.VsSaaS.TunnelService
{
    /// <summary>
    /// Wraps a HybridConnectionStream to assist with performing a clean shutdown.
    /// </summary>
    internal class RelayStream : Stream
    {
        private readonly HybridConnectionStream stream;
        private bool closedFromOtherSide;
        private bool disposed;

        public RelayStream(HybridConnectionStream stream)
        {
            this.stream = Requires.NotNull(stream, nameof(stream));
        }

        /// <summary>
        /// Disposes the stream.
        /// </summary>
        /// <remarks>
        /// A relay stream must not be disposed concurrently with a read operation --
        /// the HybridConnectionStream Shutdown() method throws an exception in that case.
        /// </remarks>
        protected override void Dispose(bool disposing)
        {
            if (!this.disposed)
            {
                this.disposed = true;

                try
                {
                    // HybridConnectionStream has unusual disposal requirements. The side that
                    // initiates the close must call the Shutdown() method first then do a read
                    // operation (that returns 0 bytes), before finally calling Dispose(). The side
                    // that doesn't initiate the close must detect a 0-byte read and call Dispose()
                    // in response to that to complete the close handshake and unblock the other side.
                    if (!this.closedFromOtherSide)
                    {
                        this.stream.Shutdown();
                        this.stream.ReadByte();
                    }

                    this.stream.Dispose();
                }
                catch (Exception)
                {
                    // Don't throw from Dispose().
                }
            }

            base.Dispose(disposing);
        }

        private void ThrowIfDisposed()
        {
            if (this.disposed)
            {
                throw new ObjectDisposedException(GetType().Name);
            }
        }

        public override bool CanRead => this.stream.CanRead;

        public override bool CanSeek => false;

        public override bool CanTimeout => this.stream.CanTimeout;

        public override bool CanWrite => this.stream.CanWrite;

        public override long Length => this.stream.Length;

        public override long Position
        {
            get => this.stream.Position;
            set => this.stream.Position = value;
        }

        public override int ReadTimeout
        {
            get => this.stream.ReadTimeout;
            set => this.stream.ReadTimeout = value;
        }

        public override int WriteTimeout
        {
            get => this.stream.WriteTimeout;
            set => this.stream.WriteTimeout = value;
        }

        public override void Flush()
        {
            ThrowIfDisposed();

            this.stream.Flush();
        }

        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            ThrowIfDisposed();

            return this.stream.FlushAsync(CancellationToken.None);
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            ThrowIfDisposed();

            int result = this.stream.Read(buffer, offset, count);
            if (result == 0)
            {
                this.closedFromOtherSide = true;
            }

            return result;
        }

        public override async Task<int> ReadAsync(
            byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            ThrowIfDisposed();

            int result = await stream.ReadAsync(
                buffer.AsMemory(offset, count), CancellationToken.None);
            if (result == 0)
            {
                this.closedFromOtherSide = true;
            }

            return result;
        }

        public override int ReadByte()
        {
            ThrowIfDisposed();

            int result = this.stream.ReadByte();
            if (result == -1)
            {
                this.closedFromOtherSide = true;
            }

            return result;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            ThrowIfDisposed();

            return this.stream.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            ThrowIfDisposed();

            this.stream.SetLength(value);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            ThrowIfDisposed();

            this.stream.Write(buffer, offset, count);
        }

        public override Task WriteAsync(
            byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            ThrowIfDisposed();

            return this.stream.WriteAsync(buffer, offset, count, CancellationToken.None);
        }

        public override void WriteByte(byte value)
        {
            ThrowIfDisposed();
            this.stream.WriteByte(value);
        }
    }
}
