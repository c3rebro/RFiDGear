using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using RFiDGear.Contracts;
using RFiDGear.Infrastructure.ExtensionHost;
using RFiDGear.Models;
using RFiDGear.Services.TaskExecution;

using Xunit;

using static RFiDGear.Tests.AutoMode.AutoModeHostTests;
using static RFiDGear.Tests.AutoMode.AutoModeMainWindowTests;

namespace RFiDGear.Tests.AutoMode
{
    /// <summary>
    /// <see cref="ExecResult.CardUid"/>: which card a run worked on, for the runs an extension starts and for the runs
    /// of automatic mode. The service reads the card once more before its first task, and that read is the card that
    /// counts, so the tests make the scripted execution service report the chip of that read the way the real one does.
    /// These tests need the same things as the other tests that build a main window (a reachable update server).
    /// </summary>
    [Collection("SettingsFileAccess")]
    public class ExecResultCardUidTests
    {
        private const string CardA = "0123456789";
        private const string CardB = "9876543210";

        private static ExecResult Plain() =>
            new ExecResult("run", ExecutionOutcome.Success, 1, 0, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, Array.Empty<TaskOutcome>());

        // ── the record ──

        [Fact]
        public void TheCardUid_IsNullForAResultBuiltTheOldWay_AndPartOfEqualityAndToString()
        {
            var plain = Plain();
            var withUid = plain with { CardUid = "AB12" };

            Assert.Null(plain.CardUid);
            Assert.Equal("AB12", withUid.CardUid);
            Assert.NotEqual(plain, withUid);
            Assert.Contains("AB12", withUid.ToString(), StringComparison.Ordinal);
        }

        // ── which UID stands for a card ──

        [Fact]
        public void ThePrimaryUid_IsTrimmedAndUpperCase_LikeTheCredentialsOfTheReader()
        {
            Assert.Equal("ABCDEF01", ReaderSnapshotBuilder.PrimaryUidOf(Card(" abcdef01 ")));
        }

        [Fact]
        public void ThePrimaryUid_IsNull_ForNoChipAndForAChipWithoutACard()
        {
            Assert.Null(ReaderSnapshotBuilder.PrimaryUidOf(null));
            Assert.Null(ReaderSnapshotBuilder.PrimaryUidOf(new GenericChipModel()));
        }

        [Fact]
        public void ThePrimaryUid_IsThatOfTheChipItself_NotOfItsChildren()
        {
            var chip = Card("aa11");
            chip.Childs = new List<GenericChipModel> { Card("bb22") };

            Assert.Equal("AA11", ReaderSnapshotBuilder.PrimaryUidOf(chip));
        }

        // ── a run an extension starts ──

        [Fact]
        public async Task ARunTheExtensionStarted_CarriesTheUidOfTheCardTheServiceRead_AlsoInExecutionCompleted()
        {
            await WithHost(async (viewModel, _, _, exec, _) =>
            {
                exec.OnExecute = request =>
                {
                    request.ChipHydrated(Card("abcdef0123"));

                    return new TaskExecutionResult { TerminalStatus = TaskLoopTerminalStatus.Success };
                };
                ExecResult? completed = null;
                viewModel.ExtensionHost.Execution.ExecutionCompleted += (_, result) => completed = result;

                var result = await viewModel.ExtensionHost.Execution.ExecuteAllAsync(ExecutionRequest.Empty);

                Assert.Equal("ABCDEF0123", result.CardUid);
                Assert.Equal(result, completed);
            });
        }

        [Fact]
        public async Task ARunTheExtensionStarted_ThatFailsAfterItsRead_StillCarriesTheUid()
        {
            await WithHost(async (viewModel, _, _, exec, _) =>
            {
                exec.OnExecute = request =>
                {
                    request.ChipHydrated(Card(CardA));

                    throw new InvalidOperationException("A task blew up.");
                };

                var result = await viewModel.ExtensionHost.Execution.ExecuteAllAsync(ExecutionRequest.Empty);

                Assert.Equal(ExecutionFailureReason.Faulted, result.FailureReason);
                Assert.Equal(CardA, result.CardUid);
            });
        }

        [Fact]
        public async Task ARunTheExtensionStarted_ThatNeverReadsACard_HasNoUid_AndNeitherDoesARunWhoseReadFoundNone()
        {
            await WithHost(async (viewModel, _, _, exec, _) =>
            {
                exec.OnExecute = _ => new TaskExecutionResult { TerminalStatus = TaskLoopTerminalStatus.Success };
                var neverRead = await viewModel.ExtensionHost.Execution.ExecuteAllAsync(ExecutionRequest.Empty);

                exec.OnExecute = request =>
                {
                    request.ChipHydrated(new GenericChipModel());

                    return new TaskExecutionResult { TerminalStatus = TaskLoopTerminalStatus.Success };
                };
                var noCard = await viewModel.ExtensionHost.Execution.ExecuteAllAsync(ExecutionRequest.Empty);

                Assert.Null(neverRead.CardUid);
                Assert.Null(noCard.CardUid);
            });
        }

        // ── a run of automatic mode ──

        [Fact]
        public async Task EveryRunOfTheMode_NamesItsOwnCard_SoARunIdAndACardBelongTogether()
        {
            await WithHost(async (viewModel, host, provider, exec, _) =>
            {
                // The real service reads the card that is on the reader; so does this one.
                exec.OnExecute = request =>
                {
                    request.ChipHydrated(provider.Card!);

                    return new TaskExecutionResult { TerminalStatus = TaskLoopTerminalStatus.Success };
                };
                var results = new List<ExecResult>();
                host.AutoModeRunCompleted += (_, result) => results.Add(result);
                host.Start();

                provider.Card = Card(CardA);
                await viewModel.AutoMode.TickAsync();
                provider.Card = Card(CardB);
                await viewModel.AutoMode.TickAsync();

                Assert.Equal(new[] { CardA, CardB }, new[] { results[0].CardUid, results[1].CardUid });
                Assert.Equal(new[] { exec.Requests[0].RunId, exec.Requests[1].RunId }, new[] { results[0].RunId, results[1].RunId });
            });
        }

        [Fact]
        public async Task ARunOfTheMode_NamesTheCardTheServiceRead_NotTheOneThePollSaw()
        {
            await WithHost(async (viewModel, host, provider, exec, _) =>
            {
                // The card is swapped between the poll and the service's own read.
                exec.OnExecute = request =>
                {
                    request.ChipHydrated(Card(CardB));

                    return new TaskExecutionResult { TerminalStatus = TaskLoopTerminalStatus.Success };
                };
                ExecResult? reported = null;
                host.AutoModeRunCompleted += (_, result) => reported = result;
                host.Start();
                provider.Card = Card(CardA);

                await viewModel.AutoMode.TickAsync();

                Assert.Equal(CardB, reported!.CardUid);
            });
        }

        [Fact]
        public async Task ARunOfTheMode_ThatFailsAfterItsRead_StillNamesTheCard_AndOneThatNeverReadsHasNone()
        {
            await WithHost(async (viewModel, host, provider, exec, _) =>
            {
                var results = new List<ExecResult>();
                host.AutoModeRunCompleted += (_, result) => results.Add(result);
                host.Start();
                provider.Card = Card(CardA);

                // The first run fails a task after the service has read the card and the mode goes on; it is then
                // followed by a run whose service never got as far as a read.
                exec.OnExecute = request =>
                {
                    request.ChipHydrated(provider.Card!);

                    return new TaskExecutionResult { TerminalStatus = TaskLoopTerminalStatus.Failure };
                };
                await viewModel.AutoMode.TickAsync();

                provider.Card = Card(CardB);
                exec.OnExecute = _ => new TaskExecutionResult { TerminalStatus = TaskLoopTerminalStatus.Success };
                await viewModel.AutoMode.TickAsync();

                Assert.Equal(CardA, results[0].CardUid);
                Assert.Equal(ExecutionOutcome.Failure, results[0].Outcome);
                Assert.Null(results[1].CardUid);
            });
        }
    }
}
