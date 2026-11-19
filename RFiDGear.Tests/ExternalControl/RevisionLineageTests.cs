using System.Linq;

using RFiDGear.Services.ExternalControl;

using Xunit;

namespace RFiDGear.Tests.ExternalControl
{
    /// <summary>
    /// A run changes the project it runs, so the state after it is the project it started with and not another one; a state that
    /// no run led to is another project.
    /// </summary>
    public sealed class RevisionLineageTests
    {
        [Fact]
        public void AStateIsTheSameProjectAsItself_AndNotAsAnother()
        {
            var lineage = new RevisionLineage();

            Assert.True(lineage.Matches("a", "a"));
            Assert.False(lineage.Matches("a", "b"));
        }

        [Fact]
        public void TheStateAfterARun_IsTheProjectItStartedWith_FromBothSides()
        {
            var lineage = new RevisionLineage();

            lineage.Absorb("start", "after-run");

            Assert.True(lineage.Matches("after-run", "start"));
            Assert.True(lineage.Matches("start", "after-run"));
        }

        [Fact]
        public void RunsOneAfterTheOther_AreOneProject_AllTheWayBack()
        {
            var lineage = new RevisionLineage();

            lineage.Absorb("r0", "r1");
            lineage.Absorb("r1", "r2");
            lineage.Absorb("r2", "r3");

            Assert.True(lineage.Matches("r3", "r0"));
            Assert.True(lineage.Matches("r1", "r3"));
        }

        [Fact]
        public void AStateNoRunLedTo_IsAnotherProject_EvenWhenARunCameBefore()
        {
            var lineage = new RevisionLineage();
            lineage.Absorb("r0", "r1");

            // A person changed a task after the run: no run made r2.
            Assert.False(lineage.Matches("r2", "r0"));
            Assert.False(lineage.Matches("r2", "r1"));
            Assert.True(lineage.Matches("r1", "r0"));
        }

        [Fact]
        public void ARunThatEndsWhereItStarted_IsNotRecorded()
        {
            var lineage = new RevisionLineage();

            lineage.Absorb("r0", "r0");

            Assert.False(lineage.Matches("r0", "r1"));
        }

        [Fact]
        public void ARunThatEndsInAnEarlierState_CannotMakeALoop()
        {
            var lineage = new RevisionLineage();

            lineage.Absorb("a", "b");
            lineage.Absorb("b", "a");

            Assert.True(lineage.Matches("a", "b"));
            Assert.False(lineage.Matches("a", "c"));
        }

        [Fact]
        public void TheMemoryIsBounded_TheOldestStateIsForgotten_TheNewestIsNot()
        {
            var lineage = new RevisionLineage();

            foreach (var i in Enumerable.Range(0, 200))
            {
                lineage.Absorb("start-" + i, "end-" + i);
            }

            Assert.True(lineage.Matches("end-199", "start-199"));
            Assert.False(lineage.Matches("end-0", "start-0"));
        }
    }
}
