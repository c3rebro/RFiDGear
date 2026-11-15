#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

using LibLogicalAccess;
using LibLogicalAccess.Reader;

namespace RFiDGear.Services.Sam
{
    /// <summary>
    /// The PC/SC readers as LibLogicalAccess sees them. This is the only place of the SAM code that touches the native
    /// library, and it holds no logic: what it returns is judged by <see cref="SamReaderFinder"/>, which the tests run with
    /// a script. The hardware case of <c>RFiDGear.HIL.Tests</c> covers this class with a real reader.
    /// <para>
    /// The trace of LibLogicalAccess itself (<c>config.log.enabled</c> with <c>seecommunication</c>) writes every answer in
    /// hexadecimal, and the answer to SAM_GetVersion holds the serial number of the SAM. It is off unless somebody switches it on,
    /// and nothing here can redact it: leave it off while a SAM is in a reader (<c>SAM-AV3.md</c> says so as well).
    /// </para>
    /// </summary>
    internal sealed class LlaSamReaderSource : ISamReaderSource
    {
        // How long the reader gets to report a card that is in it. An empty reader costs this much time in a search.
        private const int InsertionWaitMilliseconds = 600;

        // The time a command gets: the key entry of a SAM is written to its EEPROM.
        private const int CommandTimeoutMilliseconds = 5000;

        // What LibLogicalAccess calls the chip of a MIFARE SAM AV3 (probed on the bench SAM).
        private const string SamAv3ChipType = "SAM_AV3";

        /// <inheritdoc />
        public IReadOnlyList<string> GetReaderNames()
        {
            var provider = GetProvider();
            var names = provider.getReaderList().Select(reader => reader.getName()).ToList();

            // The reader units hold only a weak reference to their provider: it must live until the names are read.
            GC.KeepAlive(provider);

            return names;
        }

        /// <inheritdoc />
        public byte[]? ReadAtr(string readerName)
        {
            // The reader units hold only a weak reference to their provider (and with it, the PC/SC context): it is kept in a local
            // that is used after the last call on the unit, so that the collector cannot take it away in the middle of the read.
            var provider = GetProvider();
            var unit = provider.getReaderList().FirstOrDefault(reader => reader.getName() == readerName);

            if (unit == null)
            {
                return null;
            }

            try
            {
                // Not connect(): the library knows the ATR as soon as it sees the card (probed: isConnected stays false), and
                // connect() opens the card, which lets the library identify the chip on its own.
                if (!(unit.connectToReader() && unit.waitInsertion(InsertionWaitMilliseconds)))
                {
                    return null;
                }

                var atr = (unit as PCSCReaderUnit)?.getATR()?.ToArray();

                return atr == null || atr.Length == 0 ? null : atr;
            }
            finally
            {
                Release(unit);
                GC.KeepAlive(provider);
            }
        }

        /// <inheritdoc />
        public ISamCardConnection? Connect(string readerName)
        {
            // The provider is kept by the connection for as long as the reader unit is used (see ReadAtr).
            var provider = GetProvider();
            var unit = provider.getReaderList().FirstOrDefault(reader => reader.getName() == readerName);

            if (unit == null)
            {
                return null;
            }

            var handedOver = false;

            try
            {
                if (!(unit.connectToReader() && unit.waitInsertion(InsertionWaitMilliseconds) && unit.connect()))
                {
                    return null;
                }

                var atr = (unit as PCSCReaderUnit)?.getATR()?.ToArray();

                // A second opinion on the card that was connected. The library may give the ATR that it saw when the card was
                // detected, and a card taken out and replaced in between would carry it over; it identifies the chip again when it
                // connects, and a chip that is not a SAM AV3 now is not talked to. (There is no way to ask the connected handle for
                // its ATR through LibLogicalAccess; that would take winscard.dll.)
                var chip = unit.getSingleChip();

                // The wrapper hands back the base types; the data transport is what keeps every status word.
                var transport = unit.getDefaultReaderCardAdapter()?.getDataTransport();

                if (atr == null || atr.Length == 0 || transport == null || chip == null || chip.getCardType() != SamAv3ChipType)
                {
                    return null;
                }

                var connection = new Connection(provider, unit, atr, new Transport(transport));
                handedOver = true;

                return connection;
            }
            finally
            {
                if (!handedOver)
                {
                    Release(unit);
                    GC.KeepAlive(provider);
                }
            }
        }

        private static ReaderProvider GetProvider() => LibraryManager.getInstance().getReaderProvider("PCSC");

        private static void Release(ReaderUnit unit)
        {
            try
            {
                unit.disconnect();
            }
            catch (Exception)
            {
                // Not connected to the card: nothing to let go of.
            }

            try
            {
                unit.disconnectFromReader();
            }
            catch (Exception)
            {
                // Not connected to the reader either.
            }
        }

        private sealed class Transport : ISamTransport
        {
            private readonly DataTransport _transport;

            internal Transport(DataTransport transport)
            {
                _transport = transport;
            }

            public byte[] Exchange(byte[] apdu)
            {
                var command = new ByteVector();

                foreach (var b in apdu)
                {
                    command.Add(b);
                }

                // Not the adapter's sendCommand: that one checks the result and throws for every status word but 9000.
                return _transport.sendCommand(command, CommandTimeoutMilliseconds).ToArray();
            }
        }

        private sealed class Connection : ISamCardConnection
        {
            // Held, and not used: the reader unit has only a weak reference to its provider.
            private readonly ReaderProvider _provider;
            private readonly ReaderUnit _unit;

            internal Connection(ReaderProvider provider, ReaderUnit unit, byte[] atr, ISamTransport transport)
            {
                _provider = provider;
                _unit = unit;
                Atr = atr;
                Transport = transport;
            }

            public byte[] Atr { get; }

            public ISamTransport Transport { get; }

            public void Dispose()
            {
                Release(_unit);
                GC.KeepAlive(_provider);
            }
        }
    }
}
