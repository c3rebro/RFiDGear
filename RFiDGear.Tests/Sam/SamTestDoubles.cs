using RFiDGear.Services.Sam;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Elatec.NET.Interfaces;
using Elatec.NET.Sam;

namespace RFiDGear.Tests.Sam
{
    /// <summary>The ATRs and answers of the tests; the SAM's are the ones its reader gave, the others are typical cards.</summary>
    internal static class SamTestData
    {
        /// <summary>ATR of the MIFARE SAM AV3 on the bench (T=1, historical bytes "MIFARE SAM AV30").</summary>
        internal static readonly byte[] SamAtr = Convert.FromHexString("3BDF18FF81F1FE43003F07834D49464152452053414D204156333011");

        /// <summary>ATR of the credential that Windows Hello makes up as a card.</summary>
        internal static readonly byte[] HelloAtr = Convert.FromHexString("3B8D0180FBA000000397425446590401CF");

        /// <summary>ATR of a DESFire card on a PC/SC reader.</summary>
        internal static readonly byte[] DesfireAtr = Convert.FromHexString("3B8180018080");

        /// <summary>
        /// An answer to SAM_GetVersion: hardware and software information as the bench SAM gives them, then bytes that are
        /// recognisable as a serial number (0xEE), then success.
        /// </summary>
        internal static byte[] VersionAnswer()
        {
            var answer = new List<byte>(Convert.FromHexString("0401010500230104010105011F01"));
            answer.AddRange(Enumerable.Repeat((byte)0xEE, 17));
            answer.Add(0x90);
            answer.Add(0x00);

            return answer.ToArray();
        }
    }

    /// <summary>A transport that records every APDU and answers from a script.</summary>
    internal sealed class ScriptedSamTransport : ISamTransport
    {
        private readonly Func<byte[], byte[]> _answer;

        internal ScriptedSamTransport(Func<byte[], byte[]> answer)
        {
            _answer = answer;
        }

        /// <summary>A transport that answers every command with the same bytes.</summary>
        internal static ScriptedSamTransport Always(params byte[] answer) => new ScriptedSamTransport(_ => answer);

        internal List<byte[]> Sent { get; } = new List<byte[]>();

        /// <summary>The managed id of the thread of the last call.</summary>
        internal int LastThread { get; private set; }

        public byte[] Exchange(byte[] apdu)
        {
            Sent.Add(apdu);
            LastThread = Environment.CurrentManagedThreadId;

            return _answer(apdu);
        }
    }

    /// <summary>A channel to a SAM that records the commands and answers from a script.</summary>
    internal sealed class ScriptedSamChannel : ISamApduChannel
    {
        private readonly Func<SamApdu, SamApduResponse> _answer;

        internal ScriptedSamChannel(Func<SamApdu, SamApduResponse> answer)
        {
            _answer = answer;
        }

        internal List<SamApdu> Sent { get; } = new List<SamApdu>();

        public Task<SamApduResponse> TransmitAsync(SamApdu command)
        {
            Sent.Add(command);

            return Task.FromResult(_answer(command));
        }
    }

    /// <summary>A card in a reader of <see cref="FakeSamReaderSource"/>.</summary>
    internal sealed class FakeSamCardConnection : ISamCardConnection
    {
        internal FakeSamCardConnection(byte[] atr, ISamTransport transport)
        {
            Atr = atr;
            Transport = transport;
        }

        public byte[] Atr { get; }

        public ISamTransport Transport { get; }

        internal bool Disposed { get; private set; }

        public void Dispose() => Disposed = true;
    }

    /// <summary>PC/SC readers with a scripted card (or none) in each of them.</summary>
    internal sealed class FakeSamReaderSource : ISamReaderSource
    {
        private readonly List<(string Name, byte[]? Atr)> _readers = new List<(string, byte[]?)>();

        /// <summary>Readers whose connect throws, by name, with the exception to throw.</summary>
        internal Dictionary<string, Exception> ConnectFails { get; } = new Dictionary<string, Exception>();

        /// <summary>Readers whose reading of the ATR throws, by name, with the exception to throw.</summary>
        internal Dictionary<string, Exception> AtrFails { get; } = new Dictionary<string, Exception>();

        /// <summary>
        /// For a reader whose card was swapped between reading the ATR and connecting: the ATR that the connection then reports.
        /// </summary>
        internal Dictionary<string, byte[]> AtrOnConnect { get; } = new Dictionary<string, byte[]>();

        /// <summary>When set, listing the readers throws it.</summary>
        internal Exception? ListFails { get; set; }

        /// <summary>The names whose ATR was read, in order. Reading it opens no card.</summary>
        internal List<string> AtrRead { get; } = new List<string>();

        /// <summary>What the card answers to a command; by default success without data.</summary>
        internal Func<byte[], byte[]> Answer { get; set; } = _ => new byte[] { 0x90, 0x00 };

        /// <summary>The names that were connected to, in order.</summary>
        internal List<string> Connected { get; } = new List<string>();

        /// <summary>Every connection that was handed out, with the transport behind it.</summary>
        internal List<(string Name, FakeSamCardConnection Connection, ScriptedSamTransport Transport)> Handed { get; } =
            new List<(string, FakeSamCardConnection, ScriptedSamTransport)>();

        /// <summary>Adds a reader; <paramref name="atr"/> is <c>null</c> for one with no card.</summary>
        internal FakeSamReaderSource With(string name, byte[]? atr)
        {
            _readers.Add((name, atr));
            return this;
        }

        public IReadOnlyList<string> GetReaderNames()
        {
            if (ListFails != null)
            {
                throw ListFails;
            }

            return _readers.Select(r => r.Name).ToList();
        }

        public byte[]? ReadAtr(string readerName)
        {
            AtrRead.Add(readerName);

            if (AtrFails.TryGetValue(readerName, out var failure))
            {
                throw failure;
            }

            return _readers.FirstOrDefault(r => r.Name == readerName).Atr;
        }

        public ISamCardConnection? Connect(string readerName)
        {
            Connected.Add(readerName);

            if (ConnectFails.TryGetValue(readerName, out var failure))
            {
                throw failure;
            }

            var reader = _readers.FirstOrDefault(r => r.Name == readerName);

            if (reader.Atr == null)
            {
                return null;
            }

            var transport = new ScriptedSamTransport(Answer);
            var connection = new FakeSamCardConnection(AtrOnConnect.TryGetValue(readerName, out var swapped) ? swapped : reader.Atr, transport);
            Handed.Add((readerName, connection, transport));

            return connection;
        }
    }
}
