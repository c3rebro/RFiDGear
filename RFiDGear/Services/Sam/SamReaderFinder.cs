#nullable enable
using System;
using System.Collections.Generic;

using Elatec.NET.Interfaces;

using Serilog;

namespace RFiDGear.Services.Sam
{
    /// <summary>A PC/SC reader with a MIFARE SAM AV3 in it.</summary>
    /// <param name="Name">The name of the reader.</param>
    /// <param name="Atr">The answer to reset of the SAM.</param>
    internal sealed record SamReaderInfo(string Name, byte[] Atr);

    /// <summary>What a search for SAMs found.</summary>
    /// <param name="Readers">The readers with a SAM in them.</param>
    /// <param name="EnumerationFailed"><c>true</c> when the PC/SC service could not be asked for its readers.</param>
    internal sealed record SamSearchResult(IReadOnlyList<SamReaderInfo> Readers, bool EnumerationFailed);

    /// <summary>
    /// A connection to a MIFARE SAM AV3. It owns the connection to the card: disposing it lets go of the reader. The host
    /// session of the SAM belongs to this connection, so a second connection never continues the first one's session.
    /// </summary>
    internal sealed class SamConnection : IDisposable
    {
        private readonly ISamCardConnection _connection;

        internal SamConnection(SamReaderInfo reader, ISamCardConnection connection)
        {
            Reader = reader;
            _connection = connection;
            Channel = new LlaSamApduChannel(connection.Transport);
        }

        /// <summary>The reader the SAM is in.</summary>
        internal SamReaderInfo Reader { get; }

        /// <summary>The way to send APDUs to the SAM.</summary>
        internal ISamApduChannel Channel { get; }

        /// <inheritdoc />
        public void Dispose() => _connection.Dispose();
    }

    /// <summary>
    /// Finds the SAMs among the PC/SC readers: a card is a SAM when its ATR says <c>MIFARE SAM AV3</c>. The search reads the
    /// ATR of each card and sends nothing to it.
    /// </summary>
    internal sealed class SamReaderFinder
    {
        // A reader that is no slot of a card reader: Windows makes it up for the credentials of Windows Hello. Left alone.
        private static readonly string[] VirtualReaders = { "Windows Hello for Business" };

        private readonly ISamReaderSource _source;

        /// <summary>Creates a finder over the readers of a source.</summary>
        /// <param name="source">The PC/SC readers.</param>
        internal SamReaderFinder(ISamReaderSource source)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
        }

        /// <summary>Looks at the card of each reader.</summary>
        /// <returns>The readers that hold a MIFARE SAM AV3, and whether the PC/SC service could be asked at all.</returns>
        internal SamSearchResult Find()
        {
            IReadOnlyList<string> names;

            try
            {
                names = _source.GetReaderNames();
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                // Only the type: the text of a library exception is not for a log (CLAUDE.md, Logging).
                Log.ForContext<SamReaderFinder>().Warning("SAM search: the PC/SC readers could not be listed ({ExceptionType}).", e.GetType().Name);
                return new SamSearchResult(Array.Empty<SamReaderInfo>(), true);
            }

            var found = new List<SamReaderInfo>();

            foreach (var name in names)
            {
                if (IsVirtual(name))
                {
                    continue;
                }

                try
                {
                    using var connection = _source.Connect(name);

                    if (connection != null && SamDetection.IsSamAv3(connection.Atr))
                    {
                        found.Add(new SamReaderInfo(name, connection.Atr));
                    }
                }
                catch (Exception e) when (e is not OperationCanceledException)
                {
                    // This reader is skipped; the others are still looked at.
                    Log.ForContext<SamReaderFinder>().Warning("SAM search: a reader could not be looked at ({ExceptionType}).", e.GetType().Name);
                }
            }

            return new SamSearchResult(found, false);
        }

        /// <summary>Connects to the SAM in a reader.</summary>
        /// <param name="readerName">The reader.</param>
        /// <returns>The connection, or <c>null</c> when there is no card in the reader or it is no MIFARE SAM AV3.</returns>
        internal SamConnection? Open(string readerName)
        {
            if (string.IsNullOrEmpty(readerName) || IsVirtual(readerName))
            {
                return null;
            }

            var connection = _source.Connect(readerName);

            if (connection == null)
            {
                return null;
            }

            // The card may have been changed since the search: no command goes to a card that is not a SAM.
            if (!SamDetection.IsSamAv3(connection.Atr))
            {
                connection.Dispose();
                return null;
            }

            return new SamConnection(new SamReaderInfo(readerName, connection.Atr), connection);
        }

        private static bool IsVirtual(string readerName)
        {
            foreach (var virtualReader in VirtualReaders)
            {
                if (readerName.Contains(virtualReader, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
