#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

using RFiDGear.Bridge;
using RFiDGear.Contracts;
using RFiDGear.Infrastructure.ExtensionHost;
using RFiDGear.Infrastructure.FileAccess;
using RFiDGear.Models;
using RFiDGear.Services.ExternalControl;
using RFiDGear.Tests.CardCheckAssistant;

using StreamJsonRpc;

using Xunit;

namespace RFiDGear.Tests.ExternalControl
{
    /// <summary>
    /// <c>loadProject</c>, <c>scanReader</c> and the notices about the project and the reader: what a request must look like
    /// before the host sees it, how the answers of the host become errors of the bridge, and what the notices carry.
    /// </summary>
    [Collection("AppDataPath")]
    public sealed class BridgeProjectAndReaderTests : IDisposable
    {
        private const string Project = @"C:\projects\one.rfPrj";

        private readonly List<IDisposable> _disposables = new List<IDisposable>();

        public void Dispose()
        {
            foreach (var disposable in Enumerable.Reverse(_disposables))
            {
                disposable.Dispose();
            }
        }

        private static readonly Func<Action, Task> RunAtOnce = action =>
        {
            action();

            return Task.CompletedTask;
        };

        private async Task<(BridgeTestClient Client, ExternalControlService Service)> ConnectAsync(IRFiDGearExtensionHost host, Func<string?>? busyActivity = null, params string[] events)
        {
            var name = "RFiDGear.Bridge.test-" + Guid.NewGuid().ToString("N");
            var service = new ExternalControlService(host, RunAtOnce, () => false, "2.0.10", name, busyActivity: busyActivity);
            _disposables.Add(service);
            Assert.True(service.Start());
            service.MarkStartupComplete();
            var client = await BridgeTestClient.ConnectAsync(name, 5000);
            _disposables.Add(client);
            await client.HelloAsync(events);

            return (client, service);
        }

        private static async Task<BridgeFault> RefusalOf(Func<Task> call)
        {
            try
            {
                await call();
            }
            catch (BridgeFault fault)
            {
                return fault;
            }

            throw new Xunit.Sdk.XunitException("The call was not refused.");
        }

        private static async Task WaitUntilAsync(Func<bool> condition)
        {
            var until = DateTime.UtcNow.AddSeconds(5);

            while (!condition())
            {
                Assert.True(DateTime.UtcNow < until, "The condition did not become true in time.");
                await Task.Delay(10);
            }
        }

        // ── What a request must look like ──

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(@"projects\one.rfPrj")]
        [InlineData("one.rfPrj")]
        [InlineData(@"C:one.rfPrj")]
        [InlineData(@"\one.rfPrj")]
        [InlineData(@"\\?\C:\projects\one.rfPrj")]
        [InlineData(@"\\.\pipe\RFiDGear")]
        [InlineData("//?/C:/projects/one.rfPrj")]
        [InlineData("//./pipe/x")]
        [InlineData(@"\??\C:\projects\one.rfPrj")]
        [InlineData("C:\\projects\\one\0.rfPrj")]
        public async Task APathThatIsNotAFullPathToAFile_IsRefusedBeforeTheHostSeesIt(string path)
        {
            var host = new FakeExtensionHost();
            var operations = new BridgeHostOperations(host);

            var fault = await RefusalOf(() => operations.LoadProjectAsync(new LoadProjectRequest { Path = path }));

            Assert.Equal(BridgeErrorCodes.InvalidRequest, fault.Code);
            Assert.Equal("path", fault.Details!["field"]);
            Assert.Empty(host.ProjectHost.LoadedPaths);

            // The answer names the field and never the value.
            Assert.DoesNotContain(path.Trim().Length == 0 ? "\u0001" : path, string.Join(",", fault.Details.Values), StringComparison.Ordinal);
        }

        [Fact]
        public async Task APathThatIsTooLong_AndAMissingRequest_AreRefused()
        {
            var host = new FakeExtensionHost();
            var operations = new BridgeHostOperations(host);
            var tooLong = @"C:\" + new string('a', BridgeProtocol.MaxPathLength);

            var longFault = await RefusalOf(() => operations.LoadProjectAsync(new LoadProjectRequest { Path = tooLong }));
            var nullFault = await RefusalOf(() => operations.LoadProjectAsync(null!));

            Assert.Equal("path", longFault.Details!["field"]);
            Assert.Equal("request", nullFault.Details!["field"]);
            Assert.Empty(host.ProjectHost.LoadedPaths);
        }

        [Fact]
        public async Task AFullPath_IsPassedOnNormalised_AndTheAnswerHasTheHostsPathAndCount()
        {
            var host = new FakeExtensionHost();
            host.ProjectHost.LoadAnswer = path => ProjectOpenResult.Ok(path.ToUpperInvariant(), 7);
            var operations = new BridgeHostOperations(host);

            var response = await operations.LoadProjectAsync(new LoadProjectRequest { Path = @"C:\projects\sub\..\one.rfPrj" });

            Assert.Equal(@"C:\projects\one.rfPrj", Assert.Single(host.ProjectHost.LoadedPaths));
            Assert.Equal(@"C:\PROJECTS\ONE.RFPRJ", response.Path);
            Assert.Equal(7, response.TaskCount);
        }

        [Fact]
        public async Task WhileAutomaticModeIsOn_NoProjectIsLoaded_AndTheHostIsNotAsked()
        {
            var host = new FakeExtensionHost();
            host.AutoModeHost.IsAutoModeEnabled = true;
            var operations = new BridgeHostOperations(host);

            var fault = await RefusalOf(() => operations.LoadProjectAsync(new LoadProjectRequest { Path = Project }));

            Assert.Equal(BridgeErrorCodes.AutoModeActive, fault.Code);
            Assert.True(fault.NeverStarted);
            Assert.Empty(host.ProjectHost.LoadedPaths);
        }

        // ── How the answers of the host become errors ──

        [Theory]
        [InlineData(ProjectOpenError.PathNotFound, BridgeErrorCodes.ProjectNotFound)]
        [InlineData(ProjectOpenError.UnsupportedFormat, BridgeErrorCodes.ProjectUnsupportedFormat)]
        [InlineData(ProjectOpenError.Busy, BridgeErrorCodes.Busy)]
        [InlineData(ProjectOpenError.Unknown, BridgeErrorCodes.ProjectLoadFailed)]
        [InlineData(ProjectOpenError.None, BridgeErrorCodes.ProjectLoadFailed)]
        public async Task EveryRefusalOfTheHost_HasACodeOfItsOwn(ProjectOpenError error, int code)
        {
            var host = new FakeExtensionHost();
            host.ProjectHost.LoadAnswer = _ => ProjectOpenResult.Fail(error);
            var operations = new BridgeHostOperations(host);

            var fault = await RefusalOf(() => operations.LoadProjectAsync(new LoadProjectRequest { Path = Project }));

            Assert.Equal(code, fault.Code);
        }

        [Fact]
        public void ARefusalOfTheHostThatIsAddedLater_ForcesADecisionHere()
        {
            // A new member falls into "load failed" in the mapper; this list makes somebody look at it first.
            Assert.Equal(
                new[] { "Busy", "None", "PathNotFound", "Unknown", "UnsupportedFormat" },
                Enum.GetNames<ProjectOpenError>().OrderBy(name => name, StringComparer.Ordinal));
            Assert.Equal(
                new[] { "CanonicalPath", "Error", "Success", "TaskCount" },
                typeof(ProjectOpenResult).GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(p => p.Name).OrderBy(name => name, StringComparer.Ordinal));
        }

        [Theory]
        [InlineData(BusyActivities.Run, BusyActivities.Run)]
        [InlineData(null, BusyActivities.Other)]
        public async Task ABusyAnswer_SaysWhatTheApplicationIsBusyWith_AsAHint(string? activity, string expected)
        {
            var host = new FakeExtensionHost();
            host.ProjectHost.LoadAnswer = _ => ProjectOpenResult.Fail(ProjectOpenError.Busy);
            var operations = new BridgeHostOperations(host, () => activity);

            var fault = await RefusalOf(() => operations.LoadProjectAsync(new LoadProjectRequest { Path = Project }));

            Assert.Equal(expected, fault.Details!["activity"]);
            Assert.True(fault.NeverStarted);
        }

        [Fact]
        public void TheActivityIsReadFromTheCoordinator_InTheOrderOfWhatMattersMost()
        {
            var idle = new ExtensionOperationCoordinator();
            Assert.Null(ExternalControlService.ActivityOf(idle));

            var executing = new ExtensionOperationCoordinator();
            Assert.True(executing.TryBeginExecution());
            Assert.Equal(BusyActivities.Run, ExternalControlService.ActivityOf(executing));

            var loading = new ExtensionOperationCoordinator();
            Assert.True(loading.TryBeginLoad());
            Assert.Equal(BusyActivities.Load, ExternalControlService.ActivityOf(loading));

            var editing = new ExtensionOperationCoordinator();
            Assert.True(editing.TryBeginProjectOperation());
            Assert.Equal(BusyActivities.Edit, ExternalControlService.ActivityOf(editing));

            var scanning = new ExtensionOperationCoordinator();
            Assert.True(scanning.TryBeginScan());
            Assert.Equal(BusyActivities.Scan, ExternalControlService.ActivityOf(scanning));
        }

        // ── scanReader ──

        [Fact]
        public async Task AScan_AnswersWhatTheReaderSees_AndABusyReaderIsAnAnswerNotAnError()
        {
            var host = new FakeExtensionHost();
            host.ReaderHost.ScriptScan(new ReaderSnapshot("Reader 1", ReaderScanState.Ready, new[] { new CredentialInfo("04AABB", 7, "MIFARE DESFire", true) }));
            host.ReaderHost.ScriptScan(new ReaderSnapshot("Reader 1", ReaderScanState.Busy, Array.Empty<CredentialInfo>()));
            var operations = new BridgeHostOperations(host);

            var ready = await operations.ScanReaderAsync();
            var busy = await operations.ScanReaderAsync();

            Assert.Equal(ReaderStates.Ready, ready.Reader.State);
            Assert.Equal("04AABB", Assert.Single(ready.Reader.Credentials).Uid);
            Assert.Equal(ReaderStates.Busy, busy.Reader.State);
            Assert.Empty(busy.Reader.Credentials);
        }

        // ── Over the pipe ──

        [Fact]
        public async Task LoadProject_OverThePipe_AnswersWithThePathAndTheCount()
        {
            var host = new FakeExtensionHost();
            host.ProjectHost.LoadAnswer = path => ProjectOpenResult.Ok(path, 4);
            var (client, _) = await ConnectAsync(host);

            var response = await client.Service.LoadProject(new LoadProjectRequest { Path = Project });

            Assert.Equal(Project, response.Path);
            Assert.Equal(4, response.TaskCount);
        }

        [Fact]
        public async Task LoadProject_OverThePipe_RefusalsHaveTheirOwnCodes_AndNeverEchoThePath()
        {
            var host = new FakeExtensionHost();
            var (client, _) = await ConnectAsync(host, () => BusyActivities.Edit);

            host.ProjectHost.LoadAnswer = _ => ProjectOpenResult.Fail(ProjectOpenError.UnsupportedFormat);
            var unsupported = await Assert.ThrowsAsync<RemoteInvocationException>(() => client.Service.LoadProject(new LoadProjectRequest { Path = Project }));

            host.ProjectHost.LoadAnswer = _ => ProjectOpenResult.Fail(ProjectOpenError.Busy);
            var busy = await Assert.ThrowsAsync<RemoteInvocationException>(() => client.Service.LoadProject(new LoadProjectRequest { Path = Project }));

            var invalid = await Assert.ThrowsAsync<RemoteInvocationException>(() => client.Service.LoadProject(new LoadProjectRequest { Path = @"relative\SECRET-name.rfPrj" }));

            Assert.Equal(BridgeErrorCodes.ProjectUnsupportedFormat, unsupported.ErrorCode);
            Assert.Equal(BridgeErrorCodes.Busy, busy.ErrorCode);
            Assert.Equal(BusyActivities.Edit, BridgeTestClient.DataOf(busy)!.Details!["activity"]);
            Assert.Equal(BridgeErrorCodes.InvalidRequest, invalid.ErrorCode);
            Assert.DoesNotContain("SECRET", invalid.Message + string.Join(",", BridgeTestClient.DataOf(invalid)!.Details!.Values), StringComparison.Ordinal);
            Assert.DoesNotContain(Project, unsupported.Message + busy.Message, StringComparison.Ordinal);
        }

        [Fact]
        public async Task ScanReader_OverThePipe_AnswersWhatTheReaderSees()
        {
            var host = new FakeExtensionHost();
            host.ReaderHost.ScriptScan(new ReaderSnapshot("Reader 1", ReaderScanState.Ready, new[] { new CredentialInfo("04AABB", 7, "MIFARE DESFire", true) }));
            var (client, _) = await ConnectAsync(host);

            var response = await client.Service.ScanReader(new ScanReaderRequest());

            Assert.Equal(ReaderStates.Ready, response.Reader.State);
            Assert.Equal("Reader 1", response.Reader.Name);
            Assert.True(response.Reader.HasCard);
        }

        [Fact]
        public async Task AReaderProviderThatFailsWhenScanned_BecomesAnInternalError_NotATextOfItsOwn()
        {
            var host = new FakeExtensionHost();
            host.ReaderHost.ScriptScanFailure(new InvalidOperationException(@"The reader at C:\SECRET-port failed."));
            var (client, _) = await ConnectAsync(host);

            var failure = await Assert.ThrowsAsync<RemoteInvocationException>(() => client.Service.ScanReader(new ScanReaderRequest()));

            Assert.Equal(BridgeErrorCodes.InternalError, failure.ErrorCode);
            Assert.DoesNotContain("SECRET", failure.Message, StringComparison.Ordinal);
        }

        // ── The notices ──

        [Fact]
        public async Task ProjectChanged_CarriesThePathAndTheCountOfTheMomentItHappened()
        {
            var host = new FakeExtensionHost();
            var (client, _) = await ConnectAsync(host, events: BridgeEvents.ProjectChanged);

            host.ProjectHost.CurrentPath = @"C:\projects\first.rfPrj";
            host.ProjectHost.TaskCount = 2;
            host.ProjectHost.RaiseProjectChanged();

            // The list moves on before the notice is sent: the notice is about the change it announces.
            host.ProjectHost.CurrentPath = @"C:\projects\second.rfPrj";
            host.ProjectHost.TaskCount = 9;

            var notice = Assert.IsType<ProjectChangedEvent>((await client.WaitForEventsAsync(1))[0]);
            Assert.Equal(@"C:\projects\first.rfPrj", notice.Path);
            Assert.Equal(2, notice.TaskCount);
        }

        [Fact]
        public async Task ProjectChanged_ForAProjectThatWasNeverSaved_HasNoPath()
        {
            var host = new FakeExtensionHost();
            var (client, _) = await ConnectAsync(host, events: BridgeEvents.ProjectChanged);
            host.ProjectHost.CurrentPath = null;
            host.ProjectHost.TaskCount = 0;

            host.ProjectHost.RaiseProjectChanged();

            var notice = Assert.IsType<ProjectChangedEvent>((await client.WaitForEventsAsync(1))[0]);
            Assert.Null(notice.Path);
            Assert.Equal(0, notice.TaskCount);
        }

        [Fact]
        public async Task ReaderChanged_CarriesThePictureOfTheReader()
        {
            var host = new FakeExtensionHost();
            var (client, _) = await ConnectAsync(host, events: BridgeEvents.ReaderChanged);

            host.ReaderHost.Show(new ReaderSnapshot("Reader 1", ReaderScanState.Ready, new[] { new CredentialInfo("04AABB", 7, "MIFARE DESFire", true) }));

            var notice = Assert.IsType<ReaderChangedEvent>((await client.WaitForEventsAsync(1))[0]);
            Assert.Equal(ReaderStates.Ready, notice.Reader.State);
            Assert.Equal("04AABB", Assert.Single(notice.Reader.Credentials).Uid);
        }

        [Fact]
        public async Task AReaderThatCannotBeRead_IsReportedAsUnknown_AndTheConnectionStaysUp()
        {
            var host = new FakeExtensionHost();
            var (client, _) = await ConnectAsync(host, events: BridgeEvents.ReaderChanged);
            host.ReaderHost.Show(new ReaderSnapshot("Reader 1", ReaderScanState.Ready, Array.Empty<CredentialInfo>()), raise: false);
            host.ReaderHost.ThrowOnRead = true;

            host.ReaderHost.Show(new ReaderSnapshot("Reader 1", ReaderScanState.NoReader, Array.Empty<CredentialInfo>()));

            var notice = Assert.IsType<ReaderChangedEvent>((await client.WaitForEventsAsync(1))[0]);
            Assert.Equal(ReaderStates.Unknown, notice.Reader.State);
            Assert.Equal(BridgeProtocol.Product, (await client.HelloAsync()).Product);
        }

        [Fact]
        public async Task GetStatus_WithAReaderThatCannotBeRead_StillAnswers_WithAnUnknownReader()
        {
            var host = new FakeExtensionHost();
            host.ReaderHost.ThrowOnRead = true;
            var (client, _) = await ConnectAsync(host);

            var status = await client.Service.GetStatus(new GetStatusRequest());

            Assert.Equal(ReaderStates.Unknown, status.Reader.State);
            Assert.False(status.Reader.HasCard);
        }

        [Fact]
        public async Task TheNoticesAreNotSent_UnlessTheClientAskedForThem()
        {
            var host = new FakeExtensionHost();
            var (client, _) = await ConnectAsync(host);

            host.ProjectHost.RaiseProjectChanged();
            host.ReaderHost.Show(new ReaderSnapshot("Reader 1", ReaderScanState.NoReader, Array.Empty<CredentialInfo>()));
            await Task.Delay(200);

            Assert.Empty(client.Events);
        }

        // ── Against the real host ──

        private sealed class RealHost : IDisposable
        {
            public RealHost()
            {
                var live = new ChipTaskHandlerModel { TaskCollection = new ObservableCollection<object>() };
                Coordinator = new ExtensionOperationCoordinator();
                Host = new HeadlessExtensionHost(
                    new DatabaseReaderWriter(),
                    new CapturingExecService(),
                    () => live,
                    action =>
                    {
                        action();

                        return Task.CompletedTask;
                    },
                    loaded =>
                    {
                        live.TaskCollection!.Clear();

                        foreach (var task in loaded?.TaskCollection ?? Enumerable.Empty<object>())
                        {
                            live.TaskCollection.Add(task);
                        }
                    },
                    "Test reader",
                    Coordinator);
            }

            public HeadlessExtensionHost Host { get; }

            public ExtensionOperationCoordinator Coordinator { get; }

            public void Dispose() => Host.Dispose();
        }

        [Fact]
        public async Task ARealProject_IsLoadedThroughTheBridge_AndTheClientIsToldAboutIt()
        {
            var path = ProjectFixtures.CreateProject(2);

            try
            {
                using var real = new RealHost();
                var (client, _) = await ConnectAsync(real.Host, () => ExternalControlService.ActivityOf(real.Coordinator), BridgeEvents.ProjectChanged);

                var response = await client.Service.LoadProject(new LoadProjectRequest { Path = path });
                var notice = Assert.IsType<ProjectChangedEvent>((await client.WaitForEventsAsync(1))[0]);
                var status = await client.Service.GetStatus(new GetStatusRequest());

                Assert.Equal(Path.GetFullPath(path), response.Path);
                Assert.Equal(2, response.TaskCount);
                Assert.Equal(Path.GetFullPath(path), notice.Path);
                Assert.Equal(2, notice.TaskCount);
                Assert.True(status.Project.Loaded);
                Assert.Equal(2, status.Project.TaskCount);
            }
            finally
            {
                TryDelete(path);
            }
        }

        [Fact]
        public async Task ARealHostThatIsBusy_RefusesTheLoad_AndSaysWhatItIsBusyWith()
        {
            var path = ProjectFixtures.CreateProject(1);

            try
            {
                using var real = new RealHost();
                var (client, _) = await ConnectAsync(real.Host, () => ExternalControlService.ActivityOf(real.Coordinator));
                Assert.True(real.Coordinator.TryBeginExecution());

                var failure = await Assert.ThrowsAsync<RemoteInvocationException>(() => client.Service.LoadProject(new LoadProjectRequest { Path = path }));

                Assert.Equal(BridgeErrorCodes.Busy, failure.ErrorCode);
                Assert.Equal(BusyActivities.Run, BridgeTestClient.DataOf(failure)!.Details!["activity"]);
                Assert.True(BridgeTestClient.DataOf(failure)!.NeverStarted);
            }
            finally
            {
                TryDelete(path);
            }
        }

        [Fact]
        public async Task AFileThatIsNotAProject_AndAFileThatIsNotThere_AreToldApart()
        {
            var notAProject = ProjectFixtures.CreateNotAnArchive();

            try
            {
                using var real = new RealHost();
                var (client, _) = await ConnectAsync(real.Host);

                var unsupported = await Assert.ThrowsAsync<RemoteInvocationException>(() => client.Service.LoadProject(new LoadProjectRequest { Path = notAProject }));
                var missing = await Assert.ThrowsAsync<RemoteInvocationException>(() => client.Service.LoadProject(new LoadProjectRequest { Path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "none.rfPrj") }));

                Assert.Equal(BridgeErrorCodes.ProjectUnsupportedFormat, unsupported.ErrorCode);
                Assert.Equal(BridgeErrorCodes.ProjectNotFound, missing.ErrorCode);
            }
            finally
            {
                TryDelete(notAProject);
            }
        }

        private static void TryDelete(string path)
        {
            try
            {
                File.Delete(path);
            }
            catch (IOException)
            {
                // A temporary file that stays behind is not a failure of the test.
            }
        }
    }
}
