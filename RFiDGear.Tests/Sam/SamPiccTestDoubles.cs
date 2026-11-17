#nullable enable
using RFiDGear.Services.Sam;

using System;
using System.Collections.Generic;
using System.Linq;

using Elatec.NET.Sam;

namespace RFiDGear.Tests.Sam
{
    /// <summary>
    /// A SAM and a card that answer a PICC authentication and a key change as they did on the bench, with the same status words and lengths and
    /// recognisable bytes in place of the cryptograms. The PC is only a relay, so these are all that the session can see. Every frame is
    /// recorded, in order, as <c>SAM &lt;hex&gt;</c> or <c>CARD &lt;hex&gt;</c>.
    /// </summary>
    internal sealed class PiccRig
    {
        // The length of the random numbers of the authentication that is under way: 8 for the native command, 16 for AES.
        private int _length = 16;

        internal PiccRig()
        {
            Sam = new ScriptedSamChannel(OnSam);
            Card = new ScriptedSamTransport(OnCard);
            Session = new SamPiccSession(Sam, Card);
        }

        /// <summary>What the card answers to its first part: the encrypted random number B.</summary>
        internal byte[] CardChallenge(int length) => Enumerable.Repeat((byte)0xB1, length).ToArray();

        /// <summary>What the SAM answers to its first part: the encrypted random numbers A and B'.</summary>
        internal byte[] SamPart1Answer(int length) => Enumerable.Repeat((byte)0xC3, 2 * length).ToArray();

        /// <summary>What the card answers to its second part: the encrypted random number A'.</summary>
        internal byte[] CardResponse(int length) => Enumerable.Repeat((byte)0xA2, length).ToArray();

        /// <summary>What the SAM answers to SAM_ChangeKeyPICC: the cryptogram.</summary>
        internal byte[] Cryptogram => Enumerable.Repeat((byte)0xD4, 24).ToArray();

        internal ScriptedSamChannel Sam { get; }

        internal ScriptedSamTransport Card { get; }

        internal SamPiccSession Session { get; }

        /// <summary>The frames in the order in which they were sent.</summary>
        internal List<string> Frames { get; } = new List<string>();

        /// <summary>How many SelectApplication commands the card answers with <c>91CA</c> before it answers right.</summary>
        internal int SelectAborts { get; set; }

        /// <summary>Replaces the answer of the SAM to a command; <c>null</c> keeps the default.</summary>
        internal Func<SamApdu, SamApduResponse?>? SamOverride { get; set; }

        /// <summary>Replaces the answer of the card to a command (data and status word); <c>null</c> keeps the default.</summary>
        internal Func<byte[], byte[]?>? CardOverride { get; set; }

        internal IEnumerable<string> SamFrames => Frames.Where(f => f.StartsWith("SAM ", StringComparison.Ordinal));

        internal IEnumerable<string> CardFrames => Frames.Where(f => f.StartsWith("CARD ", StringComparison.Ordinal));

        internal static byte[] WithStatus(byte[] data, ushort status) => data.Concat(new[] { (byte)(status >> 8), (byte)(status & 0xFF) }).ToArray();

        private SamApduResponse OnSam(SamApdu command)
        {
            Frames.Add("SAM " + Convert.ToHexString(LlaSamApduChannel.Encode(command)));

            var overridden = SamOverride?.Invoke(command);

            if (overridden != null)
            {
                return overridden;
            }

            switch (command.Ins)
            {
                case 0xCA:
                    return new SamApduResponse(Array.Empty<byte>(), 0x9000);
                case 0x0A:
                    // Part 1 has an Le, part 2 has none.
                    return command.Le.HasValue ? new SamApduResponse(SamPart1Answer(command.Data!.Length - 2), 0x90AF) : new SamApduResponse(Array.Empty<byte>(), 0x9000);
                case 0xC4:
                    return new SamApduResponse(Cryptogram, 0x9000);
                default:
                    return new SamApduResponse(Array.Empty<byte>(), 0x6D00);
            }
        }

        private byte[] OnCard(byte[] apdu)
        {
            Frames.Add("CARD " + Convert.ToHexString(apdu));

            var overridden = CardOverride?.Invoke(apdu);

            if (overridden != null)
            {
                return overridden;
            }

            switch (apdu[1])
            {
                case 0x5A:
                    if (SelectAborts > 0)
                    {
                        SelectAborts--;

                        return WithStatus(Array.Empty<byte>(), 0x91CA);
                    }

                    return WithStatus(Array.Empty<byte>(), 0x9100);
                case 0x0A:
                case 0xAA:
                    _length = apdu[1] == 0xAA ? 16 : 8;

                    return WithStatus(CardChallenge(_length), 0x91AF);
                case 0xAF:
                    return WithStatus(CardResponse(_length), 0x9100);
                case 0xC4:
                    return WithStatus(Array.Empty<byte>(), 0x9100);
                default:
                    return WithStatus(Array.Empty<byte>(), 0x911C);
            }
        }
    }
}