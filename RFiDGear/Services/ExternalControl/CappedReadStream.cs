#nullable enable
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace RFiDGear.Services.ExternalControl
{
    /// <summary>
    /// The pipe, as the JSON-RPC library reads it, with a limit on the size of a message. Every read goes through a
    /// <see cref="HeaderScanner"/>; a message that announces more than the limit, or framing that is not the protocol's,
    /// ends the connection with an <see cref="InvalidDataException"/> before the bytes reach the library, so nothing of an
    /// oversized message is buffered. Writes pass through unchanged.
    /// </summary>
    internal sealed class CappedReadStream : Stream
    {
        private readonly Stream _inner;
        private readonly HeaderScanner _scanner;

        /// <summary>Creates the stream.</summary>
        /// <param name="inner">The pipe.</param>
        /// <param name="maxMessageBytes">The longest message body that is accepted, in bytes.</param>
        internal CappedReadStream(Stream inner, int maxMessageBytes)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _scanner = new HeaderScanner(maxMessageBytes);
        }

        /// <summary>Gets whether the connection was ended because a message was too long.</summary>
        internal bool EndedBecauseTooLarge { get; private set; }

        /// <inheritdoc />
        public override bool CanRead => _inner.CanRead;

        /// <inheritdoc />
        public override bool CanSeek => false;

        /// <inheritdoc />
        public override bool CanWrite => _inner.CanWrite;

        /// <inheritdoc />
        public override long Length => throw new NotSupportedException();

        /// <inheritdoc />
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        /// <inheritdoc />
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            var read = await _inner.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            Scan(buffer.Span[..read]);

            return read;
        }

        /// <inheritdoc />
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        /// <inheritdoc />
        public override int Read(byte[] buffer, int offset, int count)
        {
            var read = _inner.Read(buffer, offset, count);
            Scan(buffer.AsSpan(offset, read));

            return read;
        }

        /// <inheritdoc />
        public override int Read(Span<byte> buffer)
        {
            var read = _inner.Read(buffer);
            Scan(buffer[..read]);

            return read;
        }

        /// <inheritdoc />
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) =>
            _inner.WriteAsync(buffer, cancellationToken);

        /// <inheritdoc />
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            _inner.WriteAsync(buffer, offset, count, cancellationToken);

        /// <inheritdoc />
        public override void Write(byte[] buffer, int offset, int count) => _inner.Write(buffer, offset, count);

        /// <inheritdoc />
        public override void Write(ReadOnlySpan<byte> buffer) => _inner.Write(buffer);

        /// <inheritdoc />
        public override Task FlushAsync(CancellationToken cancellationToken) => _inner.FlushAsync(cancellationToken);

        /// <inheritdoc />
        public override void Flush() => _inner.Flush();

        /// <inheritdoc />
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        /// <inheritdoc />
        public override void SetLength(long value) => throw new NotSupportedException();

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _inner.Dispose();
            }

            base.Dispose(disposing);
        }

        /// <inheritdoc />
        public override ValueTask DisposeAsync() => _inner.DisposeAsync();

        private void Scan(ReadOnlySpan<byte> bytes)
        {
            var result = _scanner.Feed(bytes);

            if (result == HeaderScanResult.TooLarge)
            {
                EndedBecauseTooLarge = true;
                throw new InvalidDataException("A message is longer than the limit.");
            }

            if (result == HeaderScanResult.Malformed)
            {
                throw new InvalidDataException("The framing of a message is not the one of the protocol.");
            }
        }
    }
}
