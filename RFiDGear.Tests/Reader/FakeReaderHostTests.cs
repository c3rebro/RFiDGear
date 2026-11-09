using System;
using System.Threading;
using System.Threading.Tasks;

using RFiDGear.Contracts;
using RFiDGear.Tests.CardCheckAssistant;

using Xunit;

namespace RFiDGear.Tests.Reader
{
    /// <summary>
    /// The fake reader the assistant's tests are built on keeps the contract of the real host, so a test that passes
    /// against it says something about the real one: an event for a different picture, none for a repeat, none for a scan
    /// that was busy or failed.
    /// </summary>
    public class FakeReaderHostTests
    {
        [Fact]
        public void WithoutASnapshot_ThePictureIsMadeFromTheSingleValues()
        {
            var reader = new FakeReaderHost { CurrentUID = "04AA", ChipTypeName = "DESFireEV1" };

            var credential = Assert.Single(reader.CurrentSnapshot.Credentials);
            Assert.Equal(("04AA", "DESFireEV1", true), (credential.Uid, credential.DisplayName, credential.IsPrimary));
            Assert.Equal(ReaderScanState.Ready, reader.CurrentSnapshot.State);

            reader.IsConnected = false;
            Assert.Equal(ReaderScanState.NoReader, reader.CurrentSnapshot.State);
            Assert.False(reader.CurrentSnapshot.HasCard);
        }

        [Fact]
        public void ShowingASnapshot_KeepsTheSingleValuesInStep_UntilOneOfThemIsSet()
        {
            var reader = new FakeReaderHost();
            reader.Show(Snapshots.CardOn("Reader 2", "04AA", "Mifare1K"));

            Assert.Equal(("Reader 2", "04AA", "Mifare1K", true), (reader.ReaderName, reader.CurrentUID, reader.ChipTypeName, reader.IsConnected));

            reader.CurrentUID = null;
            Assert.False(reader.CurrentSnapshot.HasCard);
            Assert.Equal("Reader 2", reader.CurrentSnapshot.ReaderName);
        }

        [Fact]
        public void ADifferentPicture_RaisesTheEventOnce_ARepeatDoesNot()
        {
            var reader = new FakeReaderHost();
            var events = 0;
            reader.ReaderStateChanged += (_, _) => events++;

            reader.Show(Snapshots.Card("04AA"));
            reader.Show(Snapshots.Card("04AA"));
            Assert.Equal(1, events);

            reader.Show(Snapshots.NoCard());
            Assert.Equal(2, events);

            reader.Show(Snapshots.Card("04BB"), raise: false);
            Assert.Equal(2, events);
            Assert.Equal("04BB", reader.CurrentUID);
        }

        [Fact]
        public async Task ScriptedScans_AreAnsweredInOrder_AndThenThePictureIsAsItWas()
        {
            var reader = new FakeReaderHost();
            reader.ScriptScan(Snapshots.Card("04AA"));
            reader.ScriptScan(Snapshots.NoCard());

            Assert.Equal(Snapshots.Card("04AA"), await reader.ScanAsync());
            Assert.Equal(Snapshots.NoCard(), await reader.ScanAsync());
            Assert.Equal(Snapshots.NoCard(), await reader.ScanAsync());
            Assert.Equal(3, reader.ScanCount);
        }

        [Fact]
        public async Task AScanThatFindsADifferentCard_RaisesTheEventOnce_AScanThatWasBusyOrFailedDoesNot()
        {
            var reader = new FakeReaderHost();
            reader.Show(Snapshots.Card("04AA"), raise: false);
            var events = 0;
            reader.ReaderStateChanged += (_, _) => events++;
            reader.ScriptScan(Snapshots.Card("04AA"));
            reader.ScriptScan(Snapshots.Card("04BB"));
            reader.ScriptScan(Snapshots.Busy());
            reader.ScriptScan(Snapshots.ReadFailed());

            await reader.ScanAsync();
            Assert.Equal(0, events);
            await reader.ScanAsync();
            Assert.Equal(1, events);
            Assert.Equal(Snapshots.Busy(), await reader.ScanAsync());
            Assert.Equal(Snapshots.ReadFailed(), await reader.ScanAsync());

            Assert.Equal(1, events);
            Assert.Equal("04BB", reader.CurrentSnapshot.Credentials[0].Uid);
        }

        [Fact]
        public async Task AScanWithAGate_WaitsForIt_AndAScriptedFailureFailsTheScan()
        {
            var reader = new FakeReaderHost();
            var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            reader.ScriptScan(Snapshots.Card("04AA"), gate);
            reader.ScriptScanFailure(new InvalidOperationException("The reader provider failed."));

            var held = reader.ScanAsync();
            await Task.Delay(50);
            Assert.False(held.IsCompleted);

            gate.SetResult(true);
            Assert.Equal(Snapshots.Card("04AA"), await held);
            await Assert.ThrowsAsync<InvalidOperationException>(() => reader.ScanAsync());
        }

        [Fact]
        public async Task ACancelledScan_IsCancelledAndNotCounted_AndAFaultyProviderFailsBothReads()
        {
            var reader = new FakeReaderHost();
            using var cancelled = new CancellationTokenSource();
            cancelled.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reader.ScanAsync(cancelled.Token));
            Assert.Equal(0, reader.ScanCount);

            reader.ThrowOnRead = true;
            Assert.Throws<InvalidOperationException>(() => reader.CurrentSnapshot);
            await Assert.ThrowsAsync<InvalidOperationException>(() => reader.ScanAsync());
        }
    }
}
