#nullable enable
using System;
using System.Globalization;
using System.Text;

namespace RFiDGear.Services.ExternalControl
{
    /// <summary>What <see cref="HeaderScanner"/> found in the bytes it was given.</summary>
    internal enum HeaderScanResult
    {
        /// <summary>Nothing wrong so far.</summary>
        Ok,

        /// <summary>A header announced a body longer than the limit.</summary>
        TooLarge,

        /// <summary>The framing is not the one of the protocol.</summary>
        Malformed,
    }

    /// <summary>
    /// Reads the framing of the messages that pass on the pipe, to refuse a message that is too long before any of it is
    /// kept. JSON-RPC over a pipe is framed with a header (<c>Content-Length: n</c>, a blank line, then n bytes), and the
    /// library trusts that number up to the size of an <c>int</c>: a client could make the host buffer two gigabytes. This
    /// class follows the bytes through, in any split, without keeping them.
    /// </summary>
    internal sealed class HeaderScanner
    {
        private const int MaxHeaderLineBytes = 256;
        private const string LengthHeader = "Content-Length:";

        private readonly long _maxBodyBytes;
        private readonly StringBuilder _line = new StringBuilder();
        private long _contentLength = -1;
        private long _remaining;
        private bool _inBody;
        private bool _sawCarriageReturn;
        private bool _lineTooLong;

        /// <summary>Creates a scanner.</summary>
        /// <param name="maxBodyBytes">The longest body that is accepted, in bytes.</param>
        internal HeaderScanner(long maxBodyBytes)
        {
            _maxBodyBytes = maxBodyBytes;
        }

        /// <summary>Follows the next bytes of the stream.</summary>
        /// <param name="data">The bytes that were read, in the order they arrive. They may end anywhere, also inside a header.</param>
        /// <returns><see cref="HeaderScanResult.Ok"/>, or why the stream must be closed.</returns>
        internal HeaderScanResult Feed(ReadOnlySpan<byte> data)
        {
            var index = 0;

            while (index < data.Length)
            {
                if (_inBody)
                {
                    var skip = (int)Math.Min(_remaining, data.Length - index);
                    index += skip;
                    _remaining -= skip;

                    if (_remaining == 0)
                    {
                        _inBody = false;
                        _contentLength = -1;
                    }

                    continue;
                }

                var value = data[index++];

                if (value == (byte)'\n')
                {
                    var result = EndOfLine();

                    if (result != HeaderScanResult.Ok)
                    {
                        return result;
                    }

                    continue;
                }

                if (value == (byte)'\r')
                {
                    // Only a line feed may follow a carriage return; another one would also slip past the limit of a line.
                    if (_sawCarriageReturn)
                    {
                        return HeaderScanResult.Malformed;
                    }

                    _sawCarriageReturn = true;
                    continue;
                }

                if (_sawCarriageReturn)
                {
                    // A carriage return that is not followed by a line feed is no framing of this protocol.
                    return HeaderScanResult.Malformed;
                }

                if (_line.Length >= MaxHeaderLineBytes)
                {
                    _lineTooLong = true;
                    return HeaderScanResult.Malformed;
                }

                _line.Append((char)value);
            }

            return _lineTooLong ? HeaderScanResult.Malformed : HeaderScanResult.Ok;
        }

        private HeaderScanResult EndOfLine()
        {
            _sawCarriageReturn = false;
            var line = _line.ToString();
            _line.Clear();

            if (line.Length == 0)
            {
                // The blank line that ends the header.
                if (_contentLength < 0)
                {
                    return HeaderScanResult.Malformed;
                }

                if (_contentLength > _maxBodyBytes)
                {
                    return HeaderScanResult.TooLarge;
                }

                _remaining = _contentLength;
                _inBody = _remaining > 0;
                _contentLength = -1;

                return HeaderScanResult.Ok;
            }

            if (line.StartsWith(LengthHeader, StringComparison.OrdinalIgnoreCase))
            {
                var number = line[LengthHeader.Length..].Trim();

                if (number.Length == 0 || number.Length > 12 || !long.TryParse(number, NumberStyles.None, CultureInfo.InvariantCulture, out var length))
                {
                    return HeaderScanResult.Malformed;
                }

                _contentLength = length;
            }

            return HeaderScanResult.Ok;
        }
    }
}
