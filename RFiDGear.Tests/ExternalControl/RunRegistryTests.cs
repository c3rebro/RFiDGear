#nullable enable
using System;

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
            Assert.Null(record.Completion.Task.Result);
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
        public void TheSourceOfTheCancellationToken_IsDisposedOnce()
        {
            var (record, _) = new RunRegistry().GetOrCreate("r-1", "hash");

            Assert.True(record.TryClaimCancellationDisposal());
            Assert.False(record.TryClaimCancellationDisposal());
            Assert.Throws<ArgumentNullException>(() => AdmissionResult.Refused(null!));
        }
    }
}
