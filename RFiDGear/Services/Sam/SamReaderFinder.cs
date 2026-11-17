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
        private readonly Action _release;
        private bool _disposed;

        /// <summary>Creates the connection to a SAM in a PC/SC reader: its channel is the APDU transport of the card, and letting go means letting go of the card.</summary>
        /// <param name="reader">The reader the SAM is in.</param>
        /// <param name="connection">The connection to the card.</param>
        internal SamConnection(SamReaderInfo reader, ISamCardConnection connection)
            : this(reader, new LlaSamApduChannel((connection ?? throw new ArgumentNullException(nameof(connection))).Transport), connection.Dispose)
        {
        }

        /// <summary>Creates the connection to a SAM that a reader of another kind opened (a socket of a TWN4).</summary>
        /// <param name="reader">The reader the SAM is in.</param>
        /// <param name="channel">The way to send APDUs to the SAM.</param>
        /// <param name="release">Lets go of the SAM (for a TWN4, powers its socket down); runs once.</param>
        internal SamConnection(SamReaderInfo reader, ISamApduChannel channel, Action release)
        {
            Reader = reader ?? throw new ArgumentNullException(nameof(reader));
            Channel = channel ?? throw new ArgumentNullException(nameof(channel));
            _release = release ?? throw new ArgumentNullException(nameof(release));
        }

        /// <summary>The reader the SAM is in.</summary>
        internal SamReaderInfo Reader { get; }

        /// <summary>The way to send APDUs to the SAM.</summary>
        internal ISamApduChannel Channel { get; }

        /// <inheritdoc />
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _release();
        }
    }

    /// <summary>
    /// Finds the SAMs among the PC/SC readers: a card is a SAM when its ATR says <c>MIFARE SAM AV3</c>. The search only reads
    /// the ATR of each card from the state of the readers; it connects to no card and sends no command to any. Only a card that
    /// is a SAM is connected to, by <see cref="Open"/>.
    /// </summary>
    internal sealed class SamReaderFinder : ISamFinder
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
        public SamSearchResult Find()
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
                    var atr = _source.ReadAtr(name);

                    if (atr != null && SamDetection.IsSamAv3(atr))
                    {
                        found.Add(new SamReaderInfo(name, atr));
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
        public SamConnection? Open(string readerName)
        {
            if (string.IsNullOrEmpty(readerName) || IsVirtual(readerName))
            {
                return null;
            }

            // No connection to a card that is not a SAM: it is looked at first, without opening it.
            if (!SamDetection.IsSamAv3(_source.ReadAtr(readerName)))
            {
                return null;
            }

            var connection = _source.Connect(readerName);

            if (connection == null)
            {
                return null;
            }

            // The card may have been changed between the two: no command goes to a card that is not a SAM.
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
