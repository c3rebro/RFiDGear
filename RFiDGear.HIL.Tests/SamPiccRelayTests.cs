#nullable enable
using RFiDGear.Infrastructure;
using RFiDGear.Infrastructure.ReaderProviders;
using RFiDGear.Models;
using RFiDGear.Services.Sam;

using System;
using System.Linq;
using System.Threading.Tasks;

using Xunit;
using Xunit.Sdk;

namespace RFiDGear.HIL.Tests
{
    /// <summary>
    /// The PC/SC reader as the relay between a MIFARE SAM AV3 in one slot of a reader and a DESFire card in the other (see
    /// <c>SAM-AV3.md</c>). It <b>authenticates only</b>: the case asks the SAM to authenticate the card with a key entry that is the last of
    /// the SAM (127), which is blank (disabled) unless somebody used it, so the SAM refuses it and the case ends at the first answer of the
    /// SAM. If the entry is in use, the card refuses the key or takes it; every one of the three outcomes is the relay working from the SAM
    /// to the card and back. Nothing is written to the SAM or to the card. A card that is no DESFire card, or no card, skips the case.
    /// <para>
    /// It needs the reader-scan tests (<c>readerScan</c> in <c>hil.settings.json</c>) and a reader pair such as the OMNIKEY 5422, with the SAM
    /// in the contact slot and the card on the contactless slot. The cases that use a key of the SAM on a card that was prepared for it are
    /// the checklist of the task commands, not this case.
    /// </para>
    /// </summary>
    [Collection("HILSam")]
    public sealed class SamPiccRelayTests
    {
        // The last key entry that a person can write: blank (disabled) unless somebody used it.
        private const string EntryText = "127:0";

        /// <summary>The relay with an AES key of the SAM.</summary>
        /// <returns>The task of the test.</returns>
        [HilReaderFact]
        public Task TheRelay_WithAnAesKeyOfTheSam_ReachesTheSamAndTheCard() => Run(DESFireKeyType.DF_KEY_AES);

        /// <summary>The relay with a DES key of the SAM: the native command of the card.</summary>
        /// <returns>The task of the test.</returns>
        [HilReaderFact]
        public Task TheRelay_WithADesKeyOfTheSam_ReachesTheSamAndTheCard() => Run(DESFireKeyType.DF_KEY_DES);

        private static Task Run(DESFireKeyType type) =>
            StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var source = new LlaSamReaderSource();
                var finder = new SamReaderFinder(source);
                var search = await Task.Run(() => finder.Find());

                if (search.EnumerationFailed)
                {
                    throw SkipException.ForSkip("The PC/SC readers could not be listed. Is the Smart Card service running?");
                }

                if (search.Readers.Count == 0)
                {
                    throw SkipException.ForSkip("No MIFARE SAM AV3 was found in a PC/SC reader. Put it into the contact slot of a reader and close the programs that use it.");
                }

                var samReader = search.Readers[0].Name;

                // The reader of the card: another PC/SC reader with a card in it (not the one that Windows Hello makes up).
                var cardReader = await Task.Run(() => source.GetReaderNames().FirstOrDefault(name =>
                    !string.Equals(name, samReader, StringComparison.OrdinalIgnoreCase)
                    && name.IndexOf("Hello", StringComparison.OrdinalIgnoreCase) < 0
                    && source.ReadAtr(name) != null));

                if (cardReader == null)
                {
                    throw SkipException.ForSkip("No card in another PC/SC reader. Put a DESFire card on the contactless slot of the reader pair.");
                }

                Assert.True(SamKeyReference.TryParse(EntryText, out var key));

                using var provider = await Task.Run(() => new LibLogicalAccessProvider(ReaderTypes.PCSC, cardReader, samReader));

                Assert.True(provider.SupportsDesfireSamKeys);

                var result = await provider.AuthenticateMifareDesfireWithSamAsync(new DesfireSamAccess(0, 0, type, key));

                if (result.Failure == SamPiccFailure.NotDesfire || result.Failure == SamPiccFailure.NoCard)
                {
                    throw SkipException.ForSkip("The card in the reader of the card is no DESFire card, or could not be connected to (" + result.Failure + "). Put a DESFire card on it.");
                }

                // Three ways in which the relay worked: the SAM refused the entry (it is blank), the card refused the key, or the key was taken.
                var worked = (result.Error == ERROR.KeyUnavailable && result.Failure == SamPiccFailure.SamRefused)
                    || (result.Error == ERROR.AuthFailure && result.Failure == SamPiccFailure.CardRefused)
                    || result.Succeeded;

                Assert.True(
                    worked,
                    "The relay ended with " + result.Error + " / " + result.Failure + " (SAM " + result.SamStatus.ToString("X4") + ", card " + result.CardStatus.ToString("X4")
                    + "). Expected the SAM to refuse the blank entry, the card to refuse the key, or the key to be taken.");
            });
    }
}
