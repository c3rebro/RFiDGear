#nullable enable
using System.Linq;

using RFiDGear.Bridge;
using RFiDGear.Services.ExternalControl;

using Xunit;

namespace RFiDGear.Tests.ExternalControl
{
    /// <summary>
    /// Which connection approved the card that a run of automatic mode was made for: the table that remembers it, and the origin of a
    /// run that the resolver finds from it. The window runs the card, so the run is a run of the mode; the client decided it, so it is
    /// the one the run is answered to.
    /// </summary>
    public sealed class ApprovedRunTests
    {
        [Fact]
        public void ARunThatWasNoted_IsFoundWithItsConnection_AndAnyOtherRunIsNot()
        {
            var table = new ApprovedRunTable();
            table.Add("run-1", "s-1");
            table.Add("run-2", "s-2");

            Assert.Equal("s-1", table.SessionOf("run-1"));
            Assert.Equal("s-2", table.SessionOf("run-2"));
            Assert.Null(table.SessionOf("run-3"));
            Assert.Null(table.SessionOf(null));
            Assert.Null(table.SessionOf(string.Empty));
        }

        [Fact]
        public void ARunNotedAgain_IsFoundWithTheLatestConnection()
        {
            var table = new ApprovedRunTable();
            table.Add("run-1", "s-1");
            table.Add("run-1", "s-2");

            Assert.Equal("s-2", table.SessionOf("run-1"));
        }

        [Fact]
        public void NothingIsNotedWithoutARunOrAConnection()
        {
            var table = new ApprovedRunTable();
            table.Add(string.Empty, "s-1");
            table.Add("run-1", string.Empty);

            Assert.Null(table.SessionOf("run-1"));
            Assert.Null(table.SessionOf(string.Empty));
        }

        [Fact]
        public void OnlyTheLatestRunsAreRemembered_TheOldestAreForgotten()
        {
            var table = new ApprovedRunTable();

            foreach (var i in Enumerable.Range(0, ApprovedRunTable.Limit + 1))
            {
                table.Add("run-" + i, "s-1");
            }

            Assert.Null(table.SessionOf("run-0"));
            Assert.Equal("s-1", table.SessionOf("run-1"));
            Assert.Equal("s-1", table.SessionOf("run-" + ApprovedRunTable.Limit));
        }

        [Fact]
        public void TheRunOfTheWindowThatAClientApproved_SaysWhichConnection_AndStaysARunOfTheMode()
        {
            var table = new ApprovedRunTable();
            table.Add("w-1", "s-1");
            var resolver = new RunOriginResolver(null, id => id == "w-1" || id == "w-2" ? RunSources.AutoMode : null, table);

            var approved = resolver.Resolve("w-1");
            var notApproved = resolver.Resolve("w-2");

            Assert.Equal((RunSources.AutoMode, "s-1"), (approved.Source, approved.SessionId));
            Assert.Equal((RunSources.AutoMode, (string?)null), (notApproved.Source, notApproved.SessionId));
        }

        [Fact]
        public void ARunThatTheWindowHasLetGoOf_IsUnknown_EvenWhenItWasNoted()
        {
            var table = new ApprovedRunTable();
            table.Add("w-1", "s-1");
            var resolver = new RunOriginResolver(null, _ => null, table);

            var origin = resolver.Resolve("w-1");

            Assert.Equal((RunSources.Other, (string?)null), (origin.Source, origin.SessionId));
        }

        [Fact]
        public void OnlyARunOfTheModeTakesTheConnection_ARunByHandDoesNot()
        {
            var table = new ApprovedRunTable();
            table.Add("w-1", "s-1");
            var resolver = new RunOriginResolver(null, _ => RunSources.Gui, table);

            var origin = resolver.Resolve("w-1");

            Assert.Equal((RunSources.Gui, (string?)null), (origin.Source, origin.SessionId));
        }
    }
}
