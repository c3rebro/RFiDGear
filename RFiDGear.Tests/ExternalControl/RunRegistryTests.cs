#nullable enable
using System;
using System.Linq;

using RFiDGear.Bridge;
using RFiDGear.Services.ExternalControl;

using Xunit;

namespace RFiDGear.Tests.ExternalControl
{
    /// <summary>The table of requests to run: one record per id, one final result per run, and what is kept and for how long.</summary>
    public class RunRegistryTests
    {
        private static RunResultDto Result(RunRecord record, long seq) => new RunResultDto { RequestId = record.RequestId, RunId = record.RunId, ResultSeq = seq };

        private static RunRecord Running(RunRegistry registry, string id)
        {
            var (record, _) = registry.GetOrCreate(id, "hash");
            Assert.True(record.TryTransition(RunRecordState.Pending, RunRecordState.Admitting));
            Assert.True(record.TryTransition(RunRecordState.Admitting, RunRecordState.Running));

            return record;
        }

        [Fact]
        public void OneRecordPerId_AndOnlyTheCallerThatMadeItKnowsIt()
        {
            var registry = new RunRegistry();

            var (first, firstCreated) = registry.GetOrCreate("r-1", "hash");
            var (again, againCreated) = registry.GetOrCreate("r-1", "other hash");

            Assert.True(firstCreated);
            Assert.False(againCreated);
            Assert.Same(first, again);
            Assert.Equal("hash", again.PayloadHash);
            Assert.Matches("^[0-9a-f]{32}$", first.RunId);
            Assert.Same(first, registry.Find("r-1", null));
            Assert.Same(first, registry.Find(null, first.RunId));
            Assert.Null(registry.Find("r-2", null));
            Assert.Null(registry.Find(null, "nobody"));
        }

        [Fact]
        public void EveryStepOfARecord_IsTakenByOneCallerOnly()
        {
            var (record, _) = new RunRegistry().GetOrCreate("r-1", "hash");

            Assert.True(record.TryTransition(RunRecordState.Pending, RunRecordState.Admitting));
            Assert.False(record.TryTransition(RunRecordState.Pending, RunRecordState.Abandoned));
            Assert.False(record.TryTransition(RunRecordState.Pending, RunRecordState.Admitting));
            Assert.Equal(RunRecordState.Admitting, record.State);
        }

        [Fact]
        public void ARunHasOneFinalResult_EvenIfTwoCallersEndIt()
        {
            var registry = new RunRegistry();
            var record = Running(registry, "r-1");

            var first = registry.Complete(record, seq => Result(record, seq));
            var second = registry.Complete(record, seq => Result(record, seq));

            Assert.NotNull(first);
            Assert.Null(second);
            Assert.Equal(1, first!.ResultSeq);
            Assert.Equal(1, registry.LastResultSeq);
            Assert.Equal(RunRecordState.Completed, record.State);
            Assert.Same(first, registry.Read(record).Result);
            Assert.True(record.Completion.Task.IsCompleted);
            Assert.True(record.Completion.Task.Result);
        }

        [Fact]
        public void AnEndedRun_IsNotCompletedBeforeItWasRunning()
        {
            var registry = new RunRegistry();
            var (record, _) = registry.GetOrCreate("r-1", "hash");

            Assert.Null(registry.Complete(record, seq => Result(record, seq)));
            Assert.Equal(0, registry.LastResultSeq);
        }

        [Fact]
        public void ARequestThatWasRefused_IsForgotten_AndThoseWhoWaitForItsResultAreReleased()
        {
            var registry = new RunRegistry();
            var (record, _) = registry.GetOrCreate("r-1", "hash");

            registry.Forget(record);
            var (newer, created) = registry.GetOrCreate("r-1", "hash");

            Assert.True(record.Completion.Task.IsCompleted);
            Assert.False(record.Completion.Task.Result);
            Assert.True(created);
            Assert.NotSame(record, newer);
            Assert.Null(registry.Find(null, record.RunId));
        }

        [Fact]
        public void TheLaterRecordOfAnId_IsNotForgottenByTheEarlierOne()
        {
            var registry = new RunRegistry();
            var (old, _) = registry.GetOrCreate("r-1", "hash");
            registry.Forget(old);
            var (newer, _) = registry.GetOrCreate("r-1", "hash");

            registry.Forget(old);

            Assert.Same(newer, registry.Find("r-1", null));
        }

        [Fact]
        public void ResultsAreKeptUpToTheirLimit_AndIdsUpToTheirs()
        {
            var registry = new RunRegistry(resultHistory: 2, requestIdHistory: 3);
            var records = new RunRecord[5];

            for (var i = 0; i < records.Length; i++)
            {
                records[i] = Running(registry, "r-" + i);
                registry.Complete(records[i], seq => Result(records[i], seq));
            }

            // The last two keep their results; the last three are still known by their ids; the first two are gone.
            Assert.Null(registry.Read(records[2]).Result);
            Assert.NotNull(registry.Read(records[3]).Result);
            Assert.NotNull(registry.Read(records[4]).Result);
            Assert.Null(registry.Find("r-0", null));
            Assert.Null(registry.Find("r-1", null));
            Assert.Same(records[2], registry.Find("r-2", null));
            Assert.Null(registry.Find(null, records[0].RunId));
            Assert.Equal(5, registry.LastResultSeq);
        }

        [Fact]
        public void ARunThatIsStillGoingOn_IsNeverDropped()
        {
            var registry = new RunRegistry(resultHistory: 1, requestIdHistory: 1);
            var inFlight = Running(registry, "going");

            for (var i = 0; i < 4; i++)
            {
                var done = Running(registry, "done-" + i);
                registry.Complete(done, seq => Result(done, seq));
            }

            Assert.Same(inFlight, registry.Find("going", null));
            Assert.Equal(RunRecordState.Running, registry.Read(inFlight).State);
        }

        [Fact]
        public void WhatTheJournalHolds_BecomesFinalRecords_ThatAreKnownByTheirIds()
        {
            var registry = new RunRegistry();
            var (endedRun, interruptedRun) = (Guid.NewGuid().ToString("N"), Guid.NewGuid().ToString("N"));

            registry.Recover(new[] { new JournalEntry("r-1", endedRun, JournalState.Ended), new JournalEntry("r-2", interruptedRun, JournalState.Started) });

            var ended = registry.Find("r-1", null)!;
            var interrupted = registry.Find(null, interruptedRun)!;
            Assert.Equal(new[] { RunRecordState.Completed, RunRecordState.Interrupted }, new[] { ended.State, interrupted.State });
            Assert.All(new[] { ended, interrupted }, record => Assert.True(record.IsRecovered));
            Assert.All(new[] { ended, interrupted }, record => Assert.Null(registry.Read(record).Result));
            Assert.All(new[] { ended, interrupted }, record => Assert.True(record.Admission.Task.IsCompletedSuccessfully && record.Admission.Task.Result.Refusal == null));
            Assert.Equal(new[] { endedRun, interruptedRun }, new[] { ended.RunId, interrupted.RunId });
            Assert.Equal(0, registry.LastResultSeq);
        }

        [Fact]
        public void ARecoveredRecord_IsNotACompetitorOfANewRequest_AndNeverOverridesOne()
        {
            var registry = new RunRegistry();
            var (live, _) = registry.GetOrCreate("r-1", "hash");

            registry.Recover(new[] { new JournalEntry("r-1", Guid.NewGuid().ToString("N"), JournalState.Started) });

            Assert.Same(live, registry.Find("r-1", null));
            Assert.False(live.IsRecovered);
        }

        [Fact]
        public void RecoveredIdsCountTowardsTheHistory_LikeThoseOfThisProcess()
        {
            var registry = new RunRegistry(resultHistory: 2, requestIdHistory: 3);
            var entries = Enumerable.Range(1, 5).Select(i => new JournalEntry("r-" + i, Guid.NewGuid().ToString("N"), JournalState.Ended)).ToList();

            registry.Recover(entries);

            Assert.Null(registry.Find("r-1", null));
            Assert.Null(registry.Find("r-2", null));
            Assert.NotNull(registry.Find("r-3", null));
            Assert.NotNull(registry.Find("r-5", null));
            Assert.Null(registry.Find(null, entries[0].RunId));
            Assert.Throws<ArgumentNullException>(() => registry.Recover(null!));
        }

        // â”€â”€ The log of results â”€â”€

        private static RunResultDto Unrequested(long seq) => new RunResultDto { RunId = "auto-" + seq, Source = RunSources.AutoMode, ResultSeq = seq };

        [Fact]
        public void TheResultsOfClientsAndOfTheMode_ShareOneSequence_AndAreListedInTheOrderTheyEnded()
        {
            var registry = new RunRegistry();
            var record = Running(registry, "r-1");

            var viaClient = registry.Complete(record, seq => Result(record, seq));
            var viaMode = registry.RecordUnrequested(Unrequested);
            var (all, gap, last, future) = registry.ListResults(0, 0);

            Assert.Equal(1, viaClient!.ResultSeq);
            Assert.Equal(2, viaMode.ResultSeq);
            Assert.Equal(new[] { 1L, 2L }, all.Select(r => r.ResultSeq));
            Assert.Same(viaMode, all[1]);
            Assert.False(gap);
            Assert.False(future);
            Assert.Equal(2, last);
            Assert.Equal(2, registry.LastResultSeq);
        }

        [Fact]
        public void AListStartsAfterTheNumberAsked_AndALimitCutsItButNotTheNumberOfTheLatest()
        {
            var registry = new RunRegistry();

            for (var i = 0; i < 5; i++)
            {
                registry.RecordUnrequested(Unrequested);
            }

            var (rest, restGap, restLast, _) = registry.ListResults(3, 0);
            var (cut, cutGap, cutLast, _) = registry.ListResults(1, 2);
            var (none, noneGap, noneLast, noneFuture) = registry.ListResults(5, 0);

            Assert.Equal(new[] { 4L, 5L }, rest.Select(r => r.ResultSeq));
            Assert.False(restGap);
            Assert.Equal(5, restLast);
            Assert.Equal(new[] { 2L, 3L }, cut.Select(r => r.ResultSeq));
            Assert.False(cutGap);
            Assert.Equal(5, cutLast);

            // Up to date: nothing after the latest, which is no gap and no error.
            Assert.Empty(none);
            Assert.False(noneGap);
            Assert.False(noneFuture);
            Assert.Equal(5, noneLast);
        }

        [Fact]
        public void WhenTheHistoryDroppedResultsTheClientHasNotSeen_TheListSaysSo()
        {
            var registry = new RunRegistry(resultHistory: 3);

            for (var i = 0; i < 6; i++)
            {
                registry.RecordUnrequested(Unrequested);
            }

            var (all, allGap, _, _) = registry.ListResults(0, 0);
            var (fromTheEdge, edgeGap, _, _) = registry.ListResults(3, 0);
            var (fromInside, insideGap, _, _) = registry.ListResults(4, 0);
            var (tooOld, oldGap, _, _) = registry.ListResults(1, 0);

            // 4, 5 and 6 are held. Asking after 3 loses nothing; asking after 0, 1 or 2 does.
            Assert.Equal(new[] { 4L, 5L, 6L }, all.Select(r => r.ResultSeq));
            Assert.True(allGap);
            Assert.False(edgeGap);
            Assert.Equal(new[] { 4L, 5L, 6L }, fromTheEdge.Select(r => r.ResultSeq));
            Assert.False(insideGap);
            Assert.Equal(new[] { 5L, 6L }, fromInside.Select(r => r.ResultSeq));
            Assert.True(oldGap);
            Assert.Equal(new[] { 4L, 5L, 6L }, tooOld.Select(r => r.ResultSeq));
        }

        [Fact]
        public void ANumberThatHasNotBeenGivenOut_IsReportedAsFromAnotherProcess_NotAsUpToDate()
        {
            var registry = new RunRegistry();
            registry.RecordUnrequested(Unrequested);

            var (results, gap, last, future) = registry.ListResults(7, 0);

            Assert.True(future);
            Assert.Empty(results);
            Assert.False(gap);
            Assert.Equal(1, last);
        }

        [Fact]
        public void AnEmptyRegistry_ListsNothing_AndNoGap()
        {
            var (results, gap, last, future) = new RunRegistry().ListResults(0, 0);

            Assert.Empty(results);
            Assert.False(gap);
            Assert.False(future);
            Assert.Equal(0, last);
        }

        [Fact]
        public void AResultOfTheModeNumbersItself_UnderTheLock_AlsoWhenManyEndAtOnce()
        {
            var registry = new RunRegistry(resultHistory: 1000);

            System.Threading.Tasks.Parallel.For(0, 200, _ => registry.RecordUnrequested(Unrequested));

            var (all, _, last, _) = registry.ListResults(0, 0);

            // Each number once, and the log in the order of the numbers.
            Assert.Equal(200, last);
            Assert.Equal(Enumerable.Range(1, 200).Select(i => (long)i), all.Select(r => r.ResultSeq));
        }
        [Fact]
        public void TheSourceOfTheCancellationToken_IsDisposedOnce()
        {
            var (record, _) = new RunRegistry().GetOrCreate("r-1", "hash");

            Assert.True(record.TryClaimCancellationDisposal());
            Assert.False(record.TryClaimCancellationDisposal());
            Assert.Throws<ArgumentNullException>(() => AdmissionResult.Refused(null!));
        }
    }
}
