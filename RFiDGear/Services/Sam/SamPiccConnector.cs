#nullable enable
using System;

using Elatec.NET.Interfaces;

namespace RFiDGear.Services.Sam
{
    /// <summary>The SAM, opened for one operation. It holds the SAM (<see cref="SamPortGate"/>) until it is disposed.</summary>
    internal sealed class SamPiccConnection : IDisposable
    {
        private readonly SamConnection _connection;
        private bool _disposed;

        /// <summary>Creates the connection; the gate of the SAM is already held and is given back by <see cref="Dispose"/>.</summary>
        /// <param name="connection">The connection to the SAM.</param>
        internal SamPiccConnection(SamConnection connection)
        {
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        }

        /// <summary>Gets the channel to the SAM.</summary>
        internal ISamApduChannel Channel => _connection.Channel;

        /// <summary>Lets go of the SAM (which resets its state: nothing that was left half done survives) and gives the gate back.</summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            try
            {
                _connection.Dispose();
            }
            finally
            {
                SamPortGate.Exit();
            }
        }
    }

    /// <summary>What opening the SAM for an operation gave.</summary>
    /// <param name="Connection">The SAM, held; or <c>null</c> when it could not be opened. The caller disposes it.</param>
    /// <param name="Failure">Why not.</param>
    internal sealed record SamPiccOpen(SamPiccConnection? Connection, SamPiccFailure Failure);

    /// <summary>Opens the SAM for one operation. The tests replace it with a script.</summary>
    internal interface ISamPiccConnector
    {
        /// <summary>Opens the SAM in the reader that is configured for it.</summary>
        /// <param name="samReaderName">The reader of the SAM, as configured; empty for none.</param>
        /// <param name="cardReaderName">The reader of the card; the SAM reader must not be the same.</param>
        /// <returns>The open SAM, or why it is not.</returns>
        SamPiccOpen Open(string? samReaderName, string? cardReaderName);
    }

    /// <summary>
    /// Opens the SAM through <see cref="SamReaderFinder"/>: it checks that the card in that reader says that it is a MIFARE SAM AV3, so that no
    /// frame of the SAM can reach another card. Nothing is sent to the card of an operation before this has succeeded.
    /// </summary>
    internal sealed class SamPiccConnector : ISamPiccConnector
    {
        private readonly SamReaderFinder _finder;

        /// <summary>Creates the connector.</summary>
        /// <param name="finder">The finder that opens the reader.</param>
        internal SamPiccConnector(SamReaderFinder finder)
        {
            _finder = finder ?? throw new ArgumentNullException(nameof(finder));
        }

        /// <inheritdoc />
        public SamPiccOpen Open(string? samReaderName, string? cardReaderName)
        {
            if (string.IsNullOrWhiteSpace(samReaderName))
            {
                return new SamPiccOpen(null, SamPiccFailure.NotConfigured);
            }

            if (!string.IsNullOrWhiteSpace(cardReaderName) && string.Equals(samReaderName.Trim(), cardReaderName!.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return new SamPiccOpen(null, SamPiccFailure.SamIsCardReader);
            }

            if (!SamPortGate.TryEnter())
            {
                return new SamPiccOpen(null, SamPiccFailure.SamBusy);
            }

            var handedOver = false;

            try
            {
                SamConnection? connection;

                try
                {
                    connection = _finder.Open(samReaderName);
                }
                catch (SamReaderException)
                {
                    return new SamPiccOpen(null, SamPiccFailure.ReaderFailure);
                }

                if (connection == null)
                {
                    return new SamPiccOpen(null, SamPiccFailure.SamNotFound);
                }

                handedOver = true;

                return new SamPiccOpen(new SamPiccConnection(connection), SamPiccFailure.None);
            }
            finally
            {
                if (!handedOver)
                {
                    SamPortGate.Exit();
                }
            }
        }
    }
}