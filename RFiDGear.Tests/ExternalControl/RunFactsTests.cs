#nullable enable
using System;
using System.Linq;

using RFiDGear.Services.ExternalControl;

using Xunit;

namespace RFiDGear.Tests.ExternalControl
{
    /// <summary>
    /// What the window notes about a run when it takes it, for the result that is made later: a fact is taken once, a run that
    /// was not noted has none, and the table never grows past the last few runs.
    /// </summary>
    public sealed class RunFactsTests
    {
        [Fact]
        public void AFact_IsTakenOnce_ByTheIdOfItsRun()
        {
            var facts = new RunFacts();
            facts.Remember("run-a", new RunFact("rev-1"));

            var first = facts.Take("run-a");
            var second = facts.Take("run-a");

            Assert.Equal(new RunFact("rev-1"), first);
            Assert.Null(second);
        }

        [Fact]
        public void ARunThatWasNotNoted_HasNoFact_AndAFactIsOfItsOwnRunOnly()
        {
            var facts = new RunFacts();
            facts.Remember("run-a", new RunFact("rev-1"));

            Assert.Null(facts.Take("run-b"));
            Assert.Equal("rev-1", facts.Take("run-a")!.ProjectRevision);
        }

        [Fact]
        public void ARunWhoseRevisionCouldNotBeMade_IsNotedAsSuch_AndIsNotTakenForAnother()
        {
            var facts = new RunFacts();
            facts.Remember("run-a", new RunFact(null));
            facts.Remember("run-b", new RunFact("rev-2"));

            Assert.Null(facts.Take("run-a")!.ProjectRevision);
            Assert.Equal("rev-2", facts.Take("run-b")!.ProjectRevision);
        }

        [Fact]
        public void NotingARunAgain_ReplacesWhatWasKnown_AndTheTableKeepsOnlyTheLastRuns()
        {
            var facts = new RunFacts();
            facts.Remember("run-0", new RunFact("old"));
            facts.Remember("run-0", new RunFact("new"));

            // Sixteen more runs push the first one out; a result that nobody asked for must not keep its fact for ever.
            foreach (var number in Enumerable.Range(1, 16))
            {
                facts.Remember("run-" + number, new RunFact("rev-" + number));
            }

            Assert.Null(facts.Take("run-0"));
            Assert.Equal("rev-16", facts.Take("run-16")!.ProjectRevision);
            Assert.Equal("rev-1", facts.Take("run-1")!.ProjectRevision);
        }

        [Fact]
        public void NobodyIsWaitingForFacts_UntilTheBridgeSaysSo()
        {
            var facts = new RunFacts();
            var before = facts.IsWanted;

            facts.IsWanted = true;
            var during = facts.IsWanted;
            facts.IsWanted = false;

            Assert.False(before);
            Assert.True(during);
            Assert.False(facts.IsWanted);
        }

        [Fact]
        public void ARunWithoutAnId_CannotBeNoted()
        {
            var facts = new RunFacts();

            Assert.Throws<ArgumentException>(() => facts.Remember(string.Empty, new RunFact("rev-1")));
            Assert.Throws<ArgumentNullException>(() => facts.Remember("run-a", null!));
        }
    }
}
