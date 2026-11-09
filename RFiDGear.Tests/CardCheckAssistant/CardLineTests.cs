using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using RFiDGear.Contracts;
using RFiDGear.Extensions.CardCheckAssistant.ViewModels;

using Xunit;

using static RFiDGear.Tests.CardCheckAssistant.ViewModelTestSupport;

namespace RFiDGear.Tests.CardCheckAssistant
{
    /// <summary>
    /// The card line and the Read card button of the page, against a reader the test scripts: what the line says for each
    /// picture of the reader, that a busy or failed look is never told as "no card", that the page scans only when asked,
    /// that the button and the run exclude each other, and that the page follows the reader event and the language.
    /// </summary>
    public class CardLineTests
    {
        private const string Uid = "04AABBCCDD";

        private static async Task WithPage(
            Func<FakeExtensionHost, CardCheckAssistantViewModel, Task> check,
            Action<FakeExtensionHost>? prepare = null)
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var host = new FakeExtensionHost();
                prepare?.Invoke(host);
                using var viewModel = Create(host);

                await check(host, viewModel);
            });
        }

        [Fact]
        public async Task TheLine_ShowsTheCardTheHostLastSaw_AndThePageNeverScansOnItsOwn()
        {
            await WithPage(
                async (host, viewModel) =>
                {
                    Assert.Equal("Card: DESFireEV1, UID " + Uid, viewModel.CardText);

                    // Opening the window, the reader event, a run elsewhere and a language switch are no reason to look.
                    await viewModel.Open();
                    host.ReaderHost.RaiseReaderStateChanged();
                    host.ExecutionHost.SetRunning(true);
                    host.ExecutionHost.SetRunning(false);
                    host.Context.ChangeCulture("de-DE");
                    await Flush();

                    Assert.Equal(0, host.ReaderHost.ScanCount);
                },
                host => host.ReaderHost.Show(Snapshots.Card(Uid), raise: false));
        }

        [Theory]
        [InlineData("nocard", "Card: no card found")]
        [InlineData("noreader", "Card: no reader")]
        [InlineData("card", "Card: DESFireEV1, UID " + Uid)]
        public async Task TheReaderEvent_RefreshesTheLine_OnTheThreadOfThePage(string picture, string expected)
        {
            await WithPage(async (host, viewModel) =>
            {
                var uiThread = Environment.CurrentManagedThreadId;
                var threads = new List<int>();
                var raised = new List<string?>();
                viewModel.PropertyChanged += (_, e) =>
                {
                    threads.Add(Environment.CurrentManagedThreadId);
                    raised.Add(e.PropertyName);
                };
                var snapshot = picture switch
                {
                    "nocard" => Snapshots.NoCard(),
                    "noreader" => Snapshots.NoReader(),
                    _ => Snapshots.Card(Uid)
                };

                await Task.Run(() => host.ReaderHost.Show(snapshot));
                await Flush();

                Assert.Equal(expected, viewModel.CardText);
                Assert.Contains(nameof(viewModel.CardText), raised);   // the bound line is told, not only the getter right
                Assert.NotEmpty(threads);
                Assert.All(threads, thread => Assert.Equal(uiThread, thread));
            }, host => host.ReaderHost.Show(Snapshots.Card("0F0F0F"), raise: false));
        }

        [Fact]
        public async Task ReadCard_ScansOnce_ShowsThatItIsReading_AndKeepsTheRunOutMeanwhile()
        {
            using var area = new TempArea();
            await WithPage(
                async (host, viewModel) =>
                {
                    var gate = NewGate();
                    host.ReaderHost.ScriptScan(Snapshots.Card(Uid), gate);
                    viewModel.ProjectPath = area.ProjectPath;

                    var read = viewModel.ReadCardCommand.ExecuteAsync();

                    Assert.Equal(1, host.ReaderHost.ScanCount);
                    Assert.True(viewModel.IsScanning);
                    Assert.Equal("Card: reading...", viewModel.CardText);
                    Assert.False(viewModel.CanReadCard);
                    Assert.False(viewModel.CanRun);
                    Assert.False(viewModel.RunCommand.CanExecute(null));

                    // A run started meanwhile would be refused as busy by the page's own scan: it does not start.
                    await viewModel.RunCommand.ExecuteAsync();
                    Assert.Empty(host.ProjectHost.LoadedPaths);

                    gate.SetResult(true);
                    await read;
                    await Flush();

                    Assert.Equal("Card: DESFireEV1, UID " + Uid, viewModel.CardText);
                    Assert.True(viewModel.CanReadCard);
                    Assert.True(viewModel.CanRun);
                    Assert.Equal(1, host.ReaderHost.ScanCount);
                },
                host => host.ReaderHost.Show(Snapshots.NoCard(), raise: false));
        }

        [Theory]
        [InlineData("busy", "Card: the reader is busy, try again in a moment")]
        [InlineData("failed", "Card: the reader could not be read")]
        public async Task ABusyOrFailedLook_IsToldAsThat_NeverAsNoCard_AndTheNextEventTakesItBack(string answer, string expected)
        {
            await WithPage(
                async (host, viewModel) =>
                {
                    host.ReaderHost.ScriptScan(answer == "busy" ? Snapshots.Busy() : Snapshots.ReadFailed());

                    await viewModel.ReadCardCommand.ExecuteAsync();

                    Assert.Equal(expected, viewModel.CardText);
                    Assert.NotEqual("Card: no card found", viewModel.CardText);

                    // The host did not take the answer as its picture, so the next change of the card shows again.
                    host.ReaderHost.Show(Snapshots.NoCard());
                    await Flush();
                    Assert.Equal("Card: no card found", viewModel.CardText);
                },
                host => host.ReaderHost.Show(Snapshots.Card(Uid), raise: false));
        }

        [Fact]
        public async Task WhenTheReaderChangedDuringTheLook_TheLineShowsThePictureOfTheHost_NotTheOldAnswer()
        {
            await WithPage(
                async (host, viewModel) =>
                {
                    var gate = NewGate();
                    host.ReaderHost.ScriptScan(Snapshots.Card("0A0A0A"), gate, store: false);
                    var read = viewModel.ReadCardCommand.ExecuteAsync();

                    host.ReaderHost.Show(Snapshots.CardOn("Other reader", "0B0B0B"));   // the user selected another reader
                    gate.SetResult(true);
                    await read;
                    await Flush();

                    Assert.Equal("Card: DESFireEV1, UID 0B0B0B", viewModel.CardText);
                },
                host => host.ReaderHost.Show(Snapshots.NoCard(), raise: false));
        }

        [Fact]
        public async Task AFailedAnswer_DoesNotOverwriteAPictureThatTheReaderEventBroughtMeanwhile()
        {
            await WithPage(
                async (host, viewModel) =>
                {
                    var gate = NewGate();
                    host.ReaderHost.ScriptScan(Snapshots.ReadFailed(), gate);
                    var read = viewModel.ReadCardCommand.ExecuteAsync();

                    host.ReaderHost.Show(Snapshots.CardOn("Other reader", "0B0B0B"));   // the user selected another reader
                    gate.SetResult(true);
                    await read;
                    await Flush();

                    Assert.Equal("Card: DESFireEV1, UID 0B0B0B", viewModel.CardText);
                },
                host => host.ReaderHost.Show(Snapshots.NoCard(), raise: false));
        }

        [Fact]
        public async Task AThrownFailure_DoesNotOverwriteAPictureThatTheReaderEventBroughtMeanwhile()
        {
            await WithPage(
                async (host, viewModel) =>
                {
                    var gate = NewGate();
                    host.ReaderHost.ScriptScanFailure(new InvalidOperationException("the provider failed"), gate);
                    var read = viewModel.ReadCardCommand.ExecuteAsync();

                    host.ReaderHost.Show(Snapshots.CardOn("Other reader", "0B0B0B"));
                    gate.SetResult(true);
                    await read;
                    await Flush();

                    Assert.Equal("Card: DESFireEV1, UID 0B0B0B", viewModel.CardText);
                },
                host => host.ReaderHost.Show(Snapshots.NoCard(), raise: false));
        }

        [Fact]
        public async Task AStateThePageDoesNotKnow_IsToldAsUnknown_LikeBusy_NeverAsThePictureOfTheHost()
        {
            await WithPage(
                async (host, viewModel) =>
                {
                    host.ReaderHost.ScriptScan(new ReaderSnapshot(Snapshots.Reader, (ReaderScanState)99, Array.Empty<CredentialInfo>()));

                    await viewModel.ReadCardCommand.ExecuteAsync();

                    Assert.Equal("Card: unknown", viewModel.CardText);
                },
                host => host.ReaderHost.Show(Snapshots.NoCard(), raise: false));
        }

        [Fact]
        public async Task AHostThatCannotBeAskedForItsPicture_IsShownAsUnknown()
        {
            await WithPage(
                (_, viewModel) =>
                {
                    Assert.Equal("Card: unknown", viewModel.CardText);

                    return Task.CompletedTask;
                },
                host => host.ReaderHost.ThrowOnRead = true);
        }

        [Fact]
        public async Task AHostThatThrowsOnTheScan_IsShownAsAFailedRead_AndThePageGoesOn()
        {
            await WithPage(async (host, viewModel) =>
            {
                host.ReaderHost.ScriptScanFailure(new InvalidOperationException("the provider failed"));

                await viewModel.ReadCardCommand.ExecuteAsync();

                Assert.Equal("Card: the reader could not be read", viewModel.CardText);
                Assert.False(viewModel.IsScanning);
                Assert.True(viewModel.CanReadCard);
            });
        }

        [Fact]
        public async Task ReadCard_IsOff_WhileTasksRun_AndWhileThePageRuns()
        {
            using var area = new TempArea();
            await WithPage(async (host, viewModel) =>
            {
                host.ExecutionHost.SetRunning(true);
                await Flush();
                Assert.False(viewModel.CanReadCard);
                Assert.False(viewModel.ReadCardCommand.CanExecute(null));
                await viewModel.ReadCardCommand.ExecuteAsync();
                Assert.Equal(0, host.ReaderHost.ScanCount);

                host.ExecutionHost.SetRunning(false);
                await Flush();
                Assert.True(viewModel.CanReadCard);

                host.ProjectHost.LoadGate = NewGate();
                viewModel.ProjectPath = area.ProjectPath;
                var run = viewModel.RunCommand.ExecuteAsync();
                await WaitUntil(() => host.ProjectHost.LoadedPaths.Count == 1, "the load to start");
                Assert.True(viewModel.IsBusy);
                Assert.False(viewModel.CanReadCard);

                host.ProjectHost.LoadGate.SetResult(true);
                await run;
                Assert.True(viewModel.CanReadCard);
            });
        }

        [Fact]
        public async Task AnotherCredential_IsCounted_NotListed()
        {
            await WithPage(
                (_, viewModel) =>
                {
                    Assert.Equal("Card: DESFireEV1_8K, UID 04AA (+1 more)", viewModel.CardText);

                    return Task.CompletedTask;
                },
                host => host.ReaderHost.Show(
                    new ReaderSnapshot(
                        Snapshots.Reader,
                        ReaderScanState.Ready,
                        new[] { new CredentialInfo("04AA", 1, "DESFireEV1_8K", true), new CredentialInfo("0102", 2, "EM4102", false) }),
                    raise: false));
        }

        [Fact]
        public async Task TheLine_FollowsALanguageSwitch()
        {
            await WithPage(
                async (host, viewModel) =>
                {
                    host.Context.ChangeCulture("de-DE");
                    await Flush();
                    Assert.Equal("Karte: DESFireEV1, UID " + Uid, viewModel.CardText);

                    host.ReaderHost.Show(Snapshots.NoCard());
                    await Flush();
                    Assert.Equal("Karte: keine Karte gefunden", viewModel.CardText);
                },
                host => host.ReaderHost.Show(Snapshots.Card(Uid), raise: false));
        }

        [Fact]
        public async Task ClosingTheWindowDuringALook_LeavesNothingBehind()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var host = new FakeExtensionHost();
                var viewModel = Create(host);
                var gate = NewGate();
                host.ReaderHost.ScriptScan(Snapshots.Card(Uid), gate);
                var read = viewModel.ReadCardCommand.ExecuteAsync();

                viewModel.Dispose();
                var changesAfterDispose = 0;
                viewModel.PropertyChanged += (_, _) => changesAfterDispose++;
                gate.SetResult(true);
                await read;
                await Flush();

                Assert.Equal(0, changesAfterDispose);
                Assert.Equal(0, host.PageSubscriberCount);
                Assert.False(viewModel.CanReadCard);
            });
        }
    }
}
