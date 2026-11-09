using System;
using System.Linq;

using RFiDGear.Contracts;
using RFiDGear.Infrastructure;
using RFiDGear.Infrastructure.ExtensionHost;
using RFiDGear.Infrastructure.ReaderProviders;
using RFiDGear.Models;

using Xunit;

namespace RFiDGear.Tests.Reader
{
    /// <summary>
    /// What the result of a read means, for each kind of reader. The providers never throw (an exception inside them comes
    /// back as a transport error), and no card is not an error of its own, so the answer depends on the kind of reader and on
    /// whether it is connected after the read.
    /// </summary>
    public class ReaderReadInterpretationTests
    {
        private static GenericChipModel Card() => new GenericChipModel("04aa", CARD_TYPE.Mifare1K);

        private static ReaderSnapshot Read(bool hasDevice, bool isPcsc, bool isConnected, ERROR result, GenericChipModel? chip) =>
            ReaderSnapshotBuilder.FromRead("Reader", hasDevice, isPcsc, isConnected, result, chip);

        [Fact]
        public void WithoutADevice_ThereIsNoReader_WhateverElseIsGiven()
        {
            var snapshot = Read(hasDevice: false, isPcsc: true, isConnected: true, ERROR.NoError, Card());

            Assert.Equal(ReaderScanState.NoReader, snapshot.State);
            Assert.Empty(snapshot.Credentials);
            Assert.Equal("Reader", snapshot.ReaderName);
        }

        [Fact]
        public void AnElatecReaderThatIsConnected_WasRead_WhenTheResultIsNoError()
        {
            var withCard = Read(true, false, true, ERROR.NoError, Card());
            var withoutCard = Read(true, false, true, ERROR.NoError, new GenericChipModel());
            var withoutChip = Read(true, false, true, ERROR.NoError, null);

            Assert.Equal((ReaderScanState.Ready, "04AA"), (withCard.State, Assert.Single(withCard.Credentials).Uid));
            Assert.Equal((ReaderScanState.Ready, false), (withoutCard.State, withoutCard.HasCard));
            Assert.Equal((ReaderScanState.Ready, false), (withoutChip.State, withoutChip.HasCard));
        }

        [Fact]
        public void AnElatecReaderThatIsNotConnected_IsNoReader_EvenWhenTheReadSaidNoError_AndTheChipStillHoldsACard()
        {
            // A reader that is not attached also answers NoError with an empty chip: only the connection tells.
            var snapshot = Read(true, false, false, ERROR.NoError, Card());

            Assert.Equal(ReaderScanState.NoReader, snapshot.State);
            Assert.Empty(snapshot.Credentials);
            Assert.Equal(ReaderScanState.NoReader, Read(true, false, false, ERROR.TransportError, null).State);
        }

        [Fact]
        public void AnElatecReaderThatIsConnected_FailedToRead_WithAnyResultButNoError()
        {
            foreach (var result in Enum.GetValues<ERROR>().Where(r => r != ERROR.NoError))
            {
                var snapshot = Read(true, false, true, result, Card());

                Assert.True(snapshot.State == ReaderScanState.ReadFailed, "Elatec, connected, " + result);
                Assert.Empty(snapshot.Credentials);
            }
        }

        [Fact]
        public void APcscReader_IsReadOnNoError_AndItsConnectionSaysNothingAboutTheReader()
        {
            // IsConnected means "a card session exists" there: false for a reader without a card.
            foreach (var connected in new[] { true, false })
            {
                var snapshot = Read(true, true, connected, ERROR.NoError, Card());

                Assert.True(snapshot.State == ReaderScanState.Ready, "PC/SC, NoError, connected " + connected);
                Assert.Equal("04AA", Assert.Single(snapshot.Credentials).Uid);
            }
        }

        [Fact]
        public void APcscReader_CountsATransportErrorAsNoCard_AndIgnoresTheStaleChip()
        {
            // The documented best effort: PC/SC answers a transport error when there is no card, and leaves the old chip.
            foreach (var connected in new[] { true, false })
            {
                var snapshot = Read(true, true, connected, ERROR.TransportError, Card());

                Assert.True(snapshot.State == ReaderScanState.Ready, "PC/SC, TransportError, connected " + connected);
                Assert.False(snapshot.HasCard);
            }
        }

        [Fact]
        public void APcscReader_FailedToRead_WithAnyOtherResult()
        {
            foreach (var result in Enum.GetValues<ERROR>().Where(r => r != ERROR.NoError && r != ERROR.TransportError))
            {
                var snapshot = Read(true, true, true, result, Card());

                Assert.True(snapshot.State == ReaderScanState.ReadFailed, "PC/SC, " + result);
                Assert.Empty(snapshot.Credentials);
            }
        }

        [Fact]
        public void OnlyThePcscProvider_SaysItIsAPcscReader()
        {
            // The PC/SC provider cannot be built without its native library, so its override is checked by shape.
            Assert.False(new ElatecNetProvider().IsPcscReader);
            Assert.Equal(typeof(LibLogicalAccessProvider), typeof(LibLogicalAccessProvider).GetProperty(nameof(ReaderDevice.IsPcscReader))!.DeclaringType);
            Assert.Equal(typeof(ReaderDevice), typeof(ElatecNetProvider).GetProperty(nameof(ReaderDevice.IsPcscReader))!.DeclaringType);
        }

        [Fact]
        public void TheAnswersForABusyHostAndAFailedRead_CarryTheNameAndNoCard()
        {
            var busy = ReaderSnapshotBuilder.Busy(null);
            var failed = ReaderSnapshotBuilder.ReadFailed("Reader");

            Assert.Equal((ReaderScanState.Busy, string.Empty), (busy.State, busy.ReaderName));
            Assert.Equal((ReaderScanState.ReadFailed, "Reader"), (failed.State, failed.ReaderName));
            Assert.Empty(busy.Credentials);
            Assert.Empty(failed.Credentials);
        }
    }
}
