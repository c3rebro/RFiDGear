using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using RFiDGear.Contracts;
using RFiDGear.Infrastructure;
using RFiDGear.Infrastructure.ExtensionHost;
using RFiDGear.Infrastructure.FileAccess;
using RFiDGear.Models;

using Xunit;

namespace RFiDGear.Tests.Reader
{
    /// <summary>A reader scan against the other things the host does: load, run, project edit, and reads of the main window.</summary>
    public class ReaderScanCoordinationTests
    {
        [Fact]
        public void AScan_IsAdmittedWhenNothingElseGoesOn_AndIsVisibleWhileItRuns()
        {
            var coordinator = new ExtensionOperationCoordinator();

            Assert.True(coordinator.TryBeginScan());

            Assert.True(coordinator.IsScanning);
            Assert.False(coordinator.IsExecuting || coordinator.IsLoading || coordinator.IsMutating);
            coordinator.EndScan();
            Assert.False(coordinator.IsScanning);
        }

        [Fact]
        public void WhileAScanRuns_NoLoadRunEditOrOtherScanIsAdmitted()
        {
            var coordinator = new ExtensionOperationCoordinator();
            Assert.True(coordinator.TryBeginScan());

            Assert.False(coordinator.TryBeginLoad());
            Assert.False(coordinator.TryBeginExecution());
            Assert.False(coordinator.TryBeginProjectOperation());
            Assert.False(coordinator.TryBeginScan());
        }

        [Fact]
        public void AScan_IsRefusedWhileALoadARunOrAnEditIsGoingOn()
        {
            var coordinator = new ExtensionOperationCoordinator();

            Assert.True(coordinator.TryBeginLoad());
            Assert.False(coordinator.TryBeginScan());
            coordinator.EndLoad();

            Assert.True(coordinator.TryBeginExecution());
            Assert.False(coordinator.TryBeginScan());
            coordinator.EndExecution();

            Assert.True(coordinator.TryBeginProjectOperation());
            Assert.False(coordinator.TryBeginScan());
            coordinator.EndProjectOperation();

            Assert.True(coordinator.TryBeginScan());
        }

        [Fact]
        public void AScan_DoesNotMakeTheHostLookLikeItIsRunningTasks()
        {
            var coordinator = new ExtensionOperationCoordinator();
            var changes = 0;
            var exec = new TaskExecHostImpl(new CapturingExecService(), new ProjectHostImpl(new DatabaseReaderWriter(), coordinator), coordinator);
            exec.IsRunningChanged += (_, _) => changes++;

            coordinator.TryBeginScan();
            coordinator.EndScan();

            Assert.False(exec.IsRunning);
            Assert.Equal(0, changes);
        }

        [Fact]
        public async Task WhileAScanRuns_ARunAnEditAndAStatusResetAreRefused_AndSayWhy()
        {
            var coordinator = new ExtensionOperationCoordinator();
            var project = new ProjectHostImpl(new DatabaseReaderWriter(), coordinator);
            var exec = new TaskExecHostImpl(new CapturingExecService(), project, coordinator);
            Assert.True(coordinator.TryBeginScan());

            var run = await Assert.ThrowsAsync<InvalidOperationException>(() => exec.ExecuteAllAsync(ExecutionRequest.Empty));
            var reset = Assert.Throws<InvalidOperationException>(() => exec.ResetStatus());
            var edit = Assert.Throws<InvalidOperationException>(() => project.New());

            Assert.Contains("reader scan", run.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("reader scan", reset.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("reader scan", edit.Message, StringComparison.OrdinalIgnoreCase);
            Assert.False(exec.IsRunning);
        }

        [Fact]
        public async Task TheHostBuiltLikeTheMainWindowBuildsIt_ScansWithItsCoordinatorAndItsGate()
        {
            var appData = Path.Combine(Path.GetTempPath(), "RFiDGear.Tests", Guid.NewGuid().ToString("N"));
            try
            {
                var coordinator = new ExtensionOperationCoordinator();
                var gate = new ReaderAccessGate();
                var live = new ChipTaskHandlerModel();
                using var host = new HeadlessExtensionHost(
                    new DatabaseReaderWriter(new ProjectManager(appData)),
                    new CapturingExecService(),
                    () => live,
                    action => { action(); return Task.CompletedTask; },
                    _ => { },
                    string.Empty,
                    coordinator,
                    getReaderName: () => "Elatec",
                    readerGate: gate);
                IRFiDGearExtensionHost api = host;

                Assert.True(coordinator.TryBeginExecution());
                Assert.Equal(ReaderScanState.Busy, (await api.Reader.ScanAsync()).State);
                coordinator.EndExecution();

                Assert.True(gate.TryEnter());
                Assert.Equal(ReaderScanState.Busy, (await api.Reader.ScanAsync()).State);
                gate.Exit();
            }
            finally
            {
                Directory.Delete(appData, recursive: true);
            }
        }

        [Fact]
        public async Task TheNameChangeOfTheHost_MakesTheReaderHostForgetWhatTheLastScanFound()
        {
            var provider = new ScriptedReadProvider();
            provider.Script(ERROR.NoError, new GenericChipModel("04aa", CARD_TYPE.Mifare1K));
            using var scope = ReaderDeviceScope.Use(provider);
            using var host = new HeadlessExtensionHost();
            var events = 0;
            host.Reader.ReaderStateChanged += (_, _) => events++;
            await host.Reader.ScanAsync();
            Assert.True(host.Reader.CurrentSnapshot.HasCard);

            // The new reader has nothing on it: the live view shows that, the forgotten scan would still show the card.
            provider.GenericChip = new GenericChipModel();
            host.NotifyReaderStateChanged();

            Assert.Equal(2, events);
            Assert.False(host.Reader.CurrentSnapshot.HasCard);
        }

        [Fact]
        public async Task TheSimpleHost_KeepsItsProjectAndItsScansApart()
        {
            var provider = new ScriptedReadProvider();
            var hold = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            provider.Script(ERROR.NoError, null, hold.Task);
            using var scope = ReaderDeviceScope.Use(provider);
            using var host = new HeadlessExtensionHost();

            var scan = host.Reader.ScanAsync();
            var started = provider.ReadStarted(1);
            Assert.Same(started, await Task.WhenAny(started, Task.Delay(TimeSpan.FromSeconds(20))));
            var edit = Assert.Throws<InvalidOperationException>(() => host.Project.New());
            hold.SetResult(true);
            await scan;

            Assert.Contains("reader scan", edit.Message, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>The gate that lets one party at a time read a card.</summary>
    public class ReaderAccessGateTests
    {
        [Fact]
        public void TheGate_CanBeTakenOnce_AndIsFreeAgainAfterExit()
        {
            var gate = new ReaderAccessGate();
            Assert.False(gate.IsHeld);

            Assert.True(gate.TryEnter());
            Assert.True(gate.IsHeld);
            Assert.False(gate.TryEnter());

            gate.Exit();
            Assert.False(gate.IsHeld);
            Assert.True(gate.TryEnter());
        }

        [Fact]
        public async Task AWaitingParty_GetsTheGateWhenTheHolderExits()
        {
            var gate = new ReaderAccessGate();
            Assert.True(gate.TryEnter());

            var waiting = gate.EnterAsync();
            Assert.False(waiting.IsCompleted);

            gate.Exit();
            await waiting;
            Assert.True(gate.IsHeld);
        }

        [Fact]
        public async Task AWaitThatIsCancelled_LeavesTheGateWithItsHolder()
        {
            var gate = new ReaderAccessGate();
            Assert.True(gate.TryEnter());
            using var cancel = new CancellationTokenSource();

            var waiting = gate.EnterAsync(cancel.Token);
            cancel.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting);
            gate.Exit();
            Assert.True(gate.TryEnter());
        }
    }
}
