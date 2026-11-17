#nullable enable
using RFiDGear.Infrastructure;
using RFiDGear.Infrastructure.ReaderProviders;
using RFiDGear.Services.Sam;

using System;
using System.Threading.Tasks;

using Xunit;
using Xunit.Sdk;

namespace RFiDGear.HIL.Tests
{
    /// <summary>
    /// The Elatec TWN4 as the relay between a MIFARE SAM AV3 in one of its SAM sockets and a DESFire card on its antenna (see
    /// <c>SAM-PICC.md</c>). Like <see cref="SamPiccRelayTests"/> it <b>authenticates only</b>: the case asks the SAM to authenticate the card with
    /// the last key entry of the SAM (127), which is blank (disabled) unless somebody used it, so the SAM refuses it and the case ends at the
    /// first answer of the SAM. If the entry is in use, the card refuses the key or takes it; every one of the three outcomes is the relay working
    /// from the SAM to the card and back. Nothing is written to the SAM or to the card.
    /// <para>
    /// It needs the reader-scan tests and the cases that change state (<c>readerScan</c> and <c>cardWrites</c> in <c>hil.settings.json</c>),
    /// because it uses the key entry 127 of the SAM once: it is meant for a SAM that you can spare and an entry that is blank. If that entry
    /// holds a real key with a usage counter, the case would count one use of it. It needs a TWN4 of hardware revision G (TWN4 MultiTech 2)
    /// with the SAM in one of its sockets and a DESFire card on it. A reader of an older revision, no SAM, a SAM in each socket and a card
    /// that is no DESFire card skip the case. Every other program and window that uses the COM port of the reader must be closed.
    /// </para>
    /// </summary>
    [Collection("HILSam")]
    public sealed class Twn4SamRelayTests
    {
        // The last key entry that a person can write: blank (disabled) unless somebody used it.
        private const string EntryText = "127:0";

        // The status word with which the bench SAM refuses a blank (disabled) key entry: the only refusal that counts as the relay working. Any
        // other status (a wrong length or data, a wrong parameter, an unknown instruction) is a fault of the frames, however it is named.
        private const ushort BlankEntryRefusal = 0x6985;

        private readonly ITestOutputHelper _output;

        /// <summary>Creates the case.</summary>
        /// <param name="output">Where the case writes what it saw.</param>
        public Twn4SamRelayTests(ITestOutputHelper output)
        {
            _output = output ?? throw new ArgumentNullException(nameof(output));
        }

        /// <summary>The relay with an AES key of the SAM.</summary>
        /// <returns>The task of the test.</returns>
        [HilCardWriteFact]
        public Task TheRelayOnATwn4_WithAnAesKeyOfTheSam_ReachesTheSamAndTheCard() => Run(DESFireKeyType.DF_KEY_AES);

        /// <summary>The relay with a DES key of the SAM: the native command of the card.</summary>
        /// <returns>The task of the test.</returns>
        [HilCardWriteFact]
        public Task TheRelayOnATwn4_WithADesKeyOfTheSam_ReachesTheSamAndTheCard() => Run(DESFireKeyType.DF_KEY_DES);

        private Task Run(DESFireKeyType type) =>
            StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                if (HilSettings.Current.ReaderKind == HilReaderKind.Pcsc)
                {
                    throw SkipException.ForSkip("\"readerKind\" in hil.settings.json asks for a PC/SC reader only; this case needs an Elatec TWN4.");
                }

                ReaderDevice.Reader = ReaderTypes.Elatec;

                if (ReaderDevice.Instance is not ElatecNetProvider provider)
                {
                    throw SkipException.ForSkip("The reader of the application is no Elatec reader.");
                }

                if (!provider.IsConnected && await provider.ConnectAsync() != ERROR.NoError)
                {
                    throw SkipException.ForSkip("No Elatec TWN4 could be connected (" + provider.LastConnectError + "). Close every other program that uses its COM port.");
                }

                Assert.True(provider.SupportsDesfireSamKeys);
                Assert.True(SamKeyReference.TryParse(EntryText, out var key));

                var result = await provider.AuthenticateMifareDesfireWithSamAsync(new DesfireSamAccess(0, 0, type, key));

                if (result.Failure == SamPiccFailure.SamNotFound || result.Failure == SamPiccFailure.SamAmbiguous)
                {
                    throw SkipException.ForSkip("The TWN4 found no single MIFARE SAM AV3 in its sockets (" + result.Failure + "): only a TWN4 of hardware revision G sees one. Put it into one socket only.");
                }

                if (result.Failure == SamPiccFailure.NotDesfire || result.Failure == SamPiccFailure.NoCard)
                {
                    throw SkipException.ForSkip("The card on the TWN4 is no DESFire card, or there is none (" + result.Failure + "). Put a DESFire card on it.");
                }

                // The card refused its first authentication command with an authentication failure (91AE): the relay never reached the SAM. A card
                // that holds no key of this kind at key 0 does that (AES on a card with a DES key); it is no pass.
                if (result.Failure == SamPiccFailure.CardRefusedBeforeSam && result.Error == ERROR.AuthFailure && result.CardStatus == 0x91AE)
                {
                    throw SkipException.ForSkip("The card refused its first authentication command (" + result.CardStatus.ToString("X4") + ") before the SAM was asked: it holds no " + type + " key at key 0 of the card. Run the other case, or use a card in the other state.");
                }

                _output.WriteLine(
                    "Relay on a TWN4 with " + type + ": " + result.Error + " / " + result.Failure + " (SAM " + result.SamStatus.ToString("X4") + ", card " + result.CardStatus.ToString("X4") + ")");

                var worked = (result.Error == ERROR.KeyUnavailable && result.Failure == SamPiccFailure.SamRefused && result.SamStatus == BlankEntryRefusal)
                    || (result.Error == ERROR.AuthFailure && result.Failure == SamPiccFailure.CardRefused)
                    || result.Succeeded;

                Assert.True(
                    worked,
                    "The relay ended with " + result.Error + " / " + result.Failure + " (SAM " + result.SamStatus.ToString("X4") + ", card " + result.CardStatus.ToString("X4")
                    + "). Expected the SAM to refuse the blank entry with 6985, the card to refuse the key, or the key to be taken. Write down any other status word.");
            });
    }
}
