using System;
using System.Collections.Generic;

using RFiDGear.Contracts;

using Xunit;

namespace RFiDGear.Tests.Reader
{
    /// <summary>
    /// The snapshot extensions get of the reader: it is a value that cannot be changed from outside, it compares by what it
    /// holds so that a change can be told from a repeat, and it cannot contradict itself.
    /// </summary>
    public class ReaderSnapshotTests
    {
        private static CredentialInfo Primary() => new CredentialInfo("04AABBCC", 0x5000, "DESFireEV1", true);

        private static CredentialInfo Other() => new CredentialInfo("0123456789", 0x40, "EM4102", false);

        [Fact]
        public void TheCredentials_AreCopied_SoLaterChangesToTheArgumentDoNotReachTheSnapshot()
        {
            var source = new List<CredentialInfo> { Primary() };
            var snapshot = new ReaderSnapshot("Reader", ReaderScanState.Ready, source);

            source[0] = Other();
            source.Add(Other());

            Assert.Equal(new[] { Primary() }, snapshot.Credentials);

            // The list handed out cannot be written to either.
            var list = Assert.IsAssignableFrom<IList<CredentialInfo>>(snapshot.Credentials);
            Assert.Throws<NotSupportedException>(() => list[0] = Other());
        }

        [Fact]
        public void NoCredentials_OrANullEntry_AreRefused()
        {
            Assert.Throws<ArgumentNullException>(() => new ReaderSnapshot("Reader", ReaderScanState.Ready, null!));
            Assert.Throws<ArgumentNullException>(() => new ReaderSnapshot("Reader", ReaderScanState.Ready, new CredentialInfo[] { Primary(), null! }));
        }

        [Theory]
        [InlineData(ReaderScanState.NoReader)]
        [InlineData(ReaderScanState.ReadFailed)]
        [InlineData(ReaderScanState.Busy)]
        public void ACardIsOnlyPossible_WhenTheReaderWasRead(ReaderScanState state)
        {
            Assert.Throws<ArgumentException>(() => new ReaderSnapshot("Reader", state, new[] { Primary() }));
            Assert.False(new ReaderSnapshot("Reader", state, Array.Empty<CredentialInfo>()).HasCard);
        }

        [Fact]
        public void HasCard_IsTrueWhenThereIsACredential_AndAReaderWithoutANameHasAnEmptyOne()
        {
            Assert.True(new ReaderSnapshot("Reader", ReaderScanState.Ready, new[] { Primary(), Other() }).HasCard);
            Assert.False(new ReaderSnapshot("Reader", ReaderScanState.Ready, Array.Empty<CredentialInfo>()).HasCard);
            Assert.Equal(string.Empty, new ReaderSnapshot(null, ReaderScanState.NoReader, Array.Empty<CredentialInfo>()).ReaderName);
        }

        [Fact]
        public void SnapshotsWithTheSameValues_AreEqual_AndSayNothingElse()
        {
            var one = new ReaderSnapshot("Reader", ReaderScanState.Ready, new[] { Primary(), Other() });
            var same = new ReaderSnapshot("Reader", ReaderScanState.Ready, new List<CredentialInfo> { Primary(), Other() });

            Assert.Equal(one, same);
            Assert.True(one.Equals((object)same));
            Assert.Equal(one.GetHashCode(), same.GetHashCode());
        }

        [Fact]
        public void ADifferentNameStateOrCredential_MakesADifferentSnapshot()
        {
            var one = new ReaderSnapshot("Reader", ReaderScanState.Ready, new[] { Primary(), Other() });

            Assert.NotEqual(one, new ReaderSnapshot("Other reader", ReaderScanState.Ready, new[] { Primary(), Other() }));
            Assert.NotEqual(one, new ReaderSnapshot("reader", ReaderScanState.Ready, new[] { Primary(), Other() }));
            Assert.NotEqual(one, new ReaderSnapshot("Reader", ReaderScanState.Ready, new[] { Primary() }));
            Assert.NotEqual(one, new ReaderSnapshot("Reader", ReaderScanState.Ready, new[] { Other(), Primary() }));
            Assert.NotEqual(one, new ReaderSnapshot("Reader", ReaderScanState.Ready, new[] { Primary(), Other() with { Uid = "0123456780" } }));
            Assert.NotEqual(
                new ReaderSnapshot("Reader", ReaderScanState.Ready, Array.Empty<CredentialInfo>()),
                new ReaderSnapshot("Reader", ReaderScanState.NoReader, Array.Empty<CredentialInfo>()));
            Assert.False(one.Equals(null));
            Assert.False(one.Equals((object)"Reader"));
        }

        [Fact]
        public void ACredential_SaysItsTypeInALog_ButNotItsIdentifier()
        {
            var text = Primary().ToString();

            Assert.Contains("DESFireEV1", text, StringComparison.Ordinal);
            Assert.DoesNotContain("04AABBCC", text, StringComparison.Ordinal);
        }

        [Fact]
        public void ToString_SaysTheStateAndTheCount_ButNoIdentifier()
        {
            var text = new ReaderSnapshot("Reader", ReaderScanState.Ready, new[] { Primary(), Other() }).ToString();

            Assert.Equal("Ready, 2 credential(s)", text);
            Assert.DoesNotContain("04AABBCC", text, StringComparison.Ordinal);
        }
    }
}
