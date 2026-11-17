#nullable enable
using RFiDGear.Services.Sam;

using System;
using System.Collections.Generic;
using System.Linq;

using Elatec.NET.Interfaces;

using Xunit;

namespace RFiDGear.Tests.Sam
{
    /// <summary>
    /// The list of the SAM setup is made of what several finders see (the PC/SC readers, the SAM sockets of a TWN4): it merges what they find,
    /// says that the search failed only when every finder failed, and opens a SAM with the finder that listed it. The finders are scripts.
    /// </summary>
    public class CompositeSamFinderTests
    {
        private static readonly byte[] Atr = SamTestData.SamAtr;

        private static SamReaderInfo Reader(string name) => new SamReaderInfo(name, Atr);

        [Fact]
        public void What_the_finders_find_is_listed_together_in_the_order_of_the_finders()
        {
            var composite = new CompositeSamFinder(new ScriptedFinder().Lists("OMNIKEY 1", "OMNIKEY 2"), new ScriptedFinder().Lists("TWN4 SAM socket 1"));

            var result = composite.Find();

            Assert.False(result.EnumerationFailed);
            Assert.Equal(new[] { "OMNIKEY 1", "OMNIKEY 2", "TWN4 SAM socket 1" }, result.Readers.Select(r => r.Name));
        }

        [Fact]
        public void Without_any_finder_there_is_nothing_and_the_search_has_not_failed()
        {
            var result = new CompositeSamFinder().Find();

            Assert.Empty(result.Readers);
            Assert.False(result.EnumerationFailed);
        }

        [Fact]
        public void The_search_has_failed_only_when_every_finder_failed()
        {
            var one = new CompositeSamFinder(new ScriptedFinder { Fails = true }, new ScriptedFinder().Lists("TWN4 SAM socket 1")).Find();
            var all = new CompositeSamFinder(new ScriptedFinder { Fails = true }, new ScriptedFinder { Fails = true }).Find();

            Assert.False(one.EnumerationFailed);
            Assert.Equal(new[] { "TWN4 SAM socket 1" }, one.Readers.Select(r => r.Name));
            Assert.True(all.EnumerationFailed);
        }

        [Fact]
        public void A_finder_that_throws_counts_as_failed_and_does_not_hide_the_others()
        {
            var composite = new CompositeSamFinder(new ScriptedFinder { Throws = new InvalidOperationException("PRIVATE-READER-TEXT") }, new ScriptedFinder().Lists("OMNIKEY 1"));
            using var logs = CapturedLogs.Install();

            var result = composite.Find();

            Assert.False(result.EnumerationFailed);
            Assert.Equal(new[] { "OMNIKEY 1" }, result.Readers.Select(r => r.Name));
            Assert.All(logs.All().Select(CapturedLogs.EverythingWrittenFor), text => Assert.DoesNotContain("PRIVATE-READER-TEXT", text, StringComparison.Ordinal));
        }

        [Fact]
        public void When_the_only_finder_throws_the_search_has_failed()
        {
            var result = new CompositeSamFinder(new ScriptedFinder { Throws = new InvalidOperationException("x") }).Find();

            Assert.Empty(result.Readers);
            Assert.True(result.EnumerationFailed);
        }

        [Fact]
        public void A_sam_is_opened_by_the_finder_that_listed_it_and_by_no_other()
        {
            var pcsc = new ScriptedFinder().Lists("OMNIKEY 1");
            var twn4 = new ScriptedFinder().Lists("TWN4 SAM socket 1");
            var composite = new CompositeSamFinder(pcsc, twn4);
            composite.Find();

            using var connection = composite.Open("TWN4 SAM socket 1");

            Assert.NotNull(connection);
            Assert.Equal(new[] { "TWN4 SAM socket 1" }, twn4.Opened);
            Assert.Empty(pcsc.Opened);
        }

        [Fact]
        public void A_name_that_nobody_listed_is_tried_with_the_finders_in_order_and_gives_the_first_connection()
        {
            var first = new ScriptedFinder();
            var second = new ScriptedFinder().Lists("OMNIKEY 1");
            var composite = new CompositeSamFinder(first, second);

            using var connection = composite.Open("OMNIKEY 1");

            Assert.NotNull(connection);
            Assert.Equal(new[] { "OMNIKEY 1" }, first.Opened);
            Assert.Equal(new[] { "OMNIKEY 1" }, second.Opened);
        }

        [Fact]
        public void A_name_that_a_later_search_no_longer_lists_is_not_sent_to_the_old_owner_only()
        {
            var twn4 = new ScriptedFinder().Lists("TWN4 SAM socket 1");
            var pcsc = new ScriptedFinder();
            var composite = new CompositeSamFinder(pcsc, twn4);
            composite.Find();
            twn4.Listed.Clear();
            composite.Find();

            var connection = composite.Open("TWN4 SAM socket 1");

            Assert.Null(connection);
            Assert.Equal(new[] { "TWN4 SAM socket 1" }, pcsc.Opened);
        }

        [Fact]
        public void Nothing_is_opened_for_an_empty_name()
        {
            var finder = new ScriptedFinder().Lists("OMNIKEY 1");

            Assert.Null(new CompositeSamFinder(finder).Open(string.Empty));
            Assert.Empty(finder.Opened);
        }

        /// <summary>A finder with a fixed answer that records what it was asked to open.</summary>
        private sealed class ScriptedFinder : ISamFinder
        {
            internal List<string> Listed { get; } = new List<string>();

            internal List<string> Opened { get; } = new List<string>();

            internal bool Fails { get; set; }

            internal Exception? Throws { get; set; }

            internal ScriptedFinder Lists(params string[] names)
            {
                Listed.AddRange(names);

                return this;
            }

            public SamSearchResult Find()
            {
                if (Throws != null)
                {
                    throw Throws;
                }

                return Fails ? new SamSearchResult(Array.Empty<SamReaderInfo>(), true) : new SamSearchResult(Listed.Select(Reader).ToList(), false);
            }

            public SamConnection? Open(string readerName)
            {
                Opened.Add(readerName);

                return Listed.Contains(readerName) ? new SamConnection(Reader(readerName), new ScriptedSamChannel(_ => throw new InvalidOperationException("not used")), () => { }) : null;
            }
        }
    }
}
