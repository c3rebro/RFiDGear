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
    /// </summary>
    internal sealed class LlaSamReaderSource : ISamReaderSource
    {
        // How long the reader gets to report a card that is in it. An empty reader costs this much time in a search.
        private const int InsertionWaitMilliseconds = 600;

        // The time a command gets: the key entry of a SAM is written to its EEPROM.
        private const int CommandTimeoutMilliseconds = 5000;

        /// <inheritdoc />
        public IReadOnlyList<string> GetReaderNames()
        {
            return GetProvider().getReaderList().Select(reader => reader.getName()).ToList();
        }

        /// <inheritdoc />
        public ISamCardConnection? Connect(string readerName)
        {
            var unit = GetProvider().getReaderList().FirstOrDefault(reader => reader.getName() == readerName);

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

                // The wrapper hands back the base types; the data transport is what keeps every status word.
                var transport = unit.getDefaultReaderCardAdapter()?.getDataTransport();

                if (atr == null || atr.Length == 0 || transport == null)
                {
                    return null;
                }

                var connection = new Connection(unit, atr, new Transport(transport));
                handedOver = true;

                return connection;
            }
            finally
            {
                if (!handedOver)
                {
                    Release(unit);
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
            private readonly ReaderUnit _unit;

            internal Connection(ReaderUnit unit, byte[] atr, ISamTransport transport)
            {
                _unit = unit;
                Atr = atr;
                Transport = transport;
            }

            public byte[] Atr { get; }

            public ISamTransport Transport { get; }

            public void Dispose() => Release(_unit);
        }
    }
}
