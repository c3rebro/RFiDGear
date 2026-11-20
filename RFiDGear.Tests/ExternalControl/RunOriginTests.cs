#nullable enable
using System;

using RFiDGear.Bridge;
using RFiDGear.Services.ExternalControl;

using Xunit;

namespace RFiDGear.Tests.ExternalControl
{
    /// <summary>
    /// Where a run came from, for the request of a key: a run of a client is in the table of the bridge, a run of the window is the
    /// one the window marked, and everything else is not placed. The rule that matters: nothing that is not a run of the client
    /// ever reads as one.
    /// </summary>
    public sealed class RunOriginTests
    {
        private static RunRecord Record(RunRegistry registry, string requestId, RunRecordState state, string? revision = "rev-1")
        {
            var (record, _) = registry.GetOrCreate(requestId, "hash-" + requestId);
            record.ProjectRevision = revision;

            if (state != RunRecordState.Pending)
            {
                Assert.True(record.TryTransition(RunRecordState.Pending, state));
            }

            return record;
        }

        [Theory]
        [InlineData("Admitting")]
        [InlineData("Running")]
        public void ARunThatAClientStarted_IsAskedAsTheClientsOwn_WithItsRequestAndRevision(string stateName)
        {
            var registry = new RunRegistry();
            var record = Record(registry, "r-1", Enum.Parse<RunRecordState>(stateName));

            var origin = new RunOriginResolver(registry, null).Resolve(record.RunId);

            Assert.Equal(new RunOrigin(RunSources.Bridge, "r-1", "rev-1"), origin);
        }

        [Fact]
        public void ARunTheHostHasLetGoOf_IsOver_EvenWhileTheRecordStillSaysRunning()
        {
            var registry = new RunRegistry();
            var record = Record(registry, "r-1", RunRecordState.Running);
            var resolver = new RunOriginResolver(registry, null);

            var whileItRuns = resolver.Resolve(record.RunId);
            record.HostEnded = true;
            var afterTheHostEnded = resolver.Resolve(record.RunId);

            Assert.Equal(RunSources.Bridge, whileItRuns.Source);
            Assert.Equal(RunOrigin.Unknown, afterTheHostEnded);
        }

        [Fact]
        public void ARunThatAClientStarted_KnowsTheConnectionItCameOver_AndTheRegistryKeepsTheFirstOne()
        {
            var registry = new RunRegistry();
            var (record, created) = registry.GetOrCreate("r-1", "hash", "s-7");
            record.ProjectRevision = "rev-1";
            Assert.True(record.TryTransition(RunRecordState.Pending, RunRecordState.Running));

            // The same request again, over another connection: it is the same run, and it stays the first connection's.
            var (again, createdAgain) = registry.GetOrCreate("r-1", "hash", "s-8");
            var origin = new RunOriginResolver(registry, null).Resolve(record.RunId);

            Assert.True(created);
            Assert.False(createdAgain);
            Assert.Same(record, again);
            Assert.Equal(new RunOrigin(RunSources.Bridge, "r-1", "rev-1", "s-7"), origin);
        }

        [Fact]
        public void ARunThatAClientStarted_WithoutARevision_SaysSo_NotWhateverIsThereNow()
        {
            var registry = new RunRegistry();
            var record = Record(registry, "r-1", RunRecordState.Running, revision: null);

            var origin = new RunOriginResolver(registry, null).Resolve(record.RunId);

            Assert.Equal(RunSources.Bridge, origin.Source);
            Assert.Null(origin.ProjectRevision);
        }

        [Theory]
        [InlineData("Pending")]
        [InlineData("Completed")]
        [InlineData("Abandoned")]
        [InlineData("Refused")]
        [InlineData("Interrupted")]
        public void ARunThatIsNotInFlight_IsNotTheClients_WhateverTheTableRemembers(string stateName)
        {
            var registry = new RunRegistry();
            var record = Record(registry, "r-1", Enum.Parse<RunRecordState>(stateName));

            var origin = new RunOriginResolver(registry, null).Resolve(record.RunId);

            Assert.Equal(RunOrigin.Unknown, origin);
        }

        [Fact]
        public void ARunThatTheBridgeDoesNotKnow_IsNotPlaced_AndSaysNothingOfAnyRequest()
        {
            var registry = new RunRegistry();
            Record(registry, "r-1", RunRecordState.Running);

            var origin = new RunOriginResolver(registry, null).Resolve("a-run-nobody-started-over-the-bridge");

            Assert.Equal(RunSources.Other, origin.Source);
            Assert.Null(origin.RequestId);
            Assert.Null(origin.ProjectRevision);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void ACommandThatNoRunStarted_IsNotPlaced(string? runId)
        {
            var registry = new RunRegistry();
            Record(registry, "r-1", RunRecordState.Running);

            var origin = new RunOriginResolver(registry, _ => RunSources.Gui).Resolve(runId);

            Assert.Equal(RunOrigin.Unknown, origin);
        }

        [Fact]
        public void ARunOfTheWindow_IsAskedWithWhoStartedIt()
        {
            var marker = new WindowRunMarker();
            var resolver = new RunOriginResolver(new RunRegistry(), marker.SourceOf);

            marker.Begin("w-1", RunSources.Gui);
            var byHand = resolver.Resolve("w-1");
            marker.End("w-1");
            marker.Begin("w-2", RunSources.AutoMode);
            var byTheMode = resolver.Resolve("w-2");

            Assert.Equal(new RunOrigin(RunSources.Gui), byHand);
            Assert.Equal(new RunOrigin(RunSources.AutoMode), byTheMode);
        }

        [Fact]
        public void ARunOfTheClient_WinsOverAMarkOfTheWindow_SoAStaleMarkCannotHideIt()
        {
            var registry = new RunRegistry();
            var record = Record(registry, "r-1", RunRecordState.Running);
            var marker = new WindowRunMarker();
            marker.Begin(record.RunId, RunSources.Gui);

            var origin = new RunOriginResolver(registry, marker.SourceOf).Resolve(record.RunId);

            Assert.Equal(RunSources.Bridge, origin.Source);
        }

        [Fact]
        public void TheMark_IsForTheRunThatIsGoingOn_AndForNoOtherIdAndNotAfterItEnded()
        {
            var marker = new WindowRunMarker();

            marker.Begin("w-1", RunSources.Gui);
            var going = marker.SourceOf("w-1");
            var other = marker.SourceOf("w-2");
            var none = marker.SourceOf(null);
            marker.End("w-1");
            var ended = marker.SourceOf("w-1");

            Assert.Equal(RunSources.Gui, going);
            Assert.Null(other);
            Assert.Null(none);
            Assert.Null(ended);
        }

        [Fact]
        public void TheEndOfAnEarlierRun_LeavesTheMarkOfTheNextRunAlone()
        {
            var marker = new WindowRunMarker();
            marker.Begin("w-1", RunSources.Gui);
            marker.Begin("w-2", RunSources.AutoMode);

            marker.End("w-1");

            Assert.Equal(RunSources.AutoMode, marker.SourceOf("w-2"));
        }
    }
}
