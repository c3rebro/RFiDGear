using RFiDGear.HIL.Tests;

using System;
using System.Threading.Tasks;

using Xunit;

namespace RFiDGear.Tests.SecureKeys
{
    /// <summary>
    /// The gate in front of the reader of the secure-key hardware cases. xunit creates the fixture of a collection also when
    /// every test in it is skipped, so the fixture itself must do nothing unless <c>secureKeys</c> was asked for: with the
    /// setting off, running only these cases must neither open the reader nor delete anything from the card. The steps that
    /// would open the reader are replaced by counters here, so none of this needs a reader.
    /// </summary>
    public sealed class HilSecureKeyFixtureTests
    {
        private const string Source = @"C:\somewhere\hil.settings.json";

        [Theory]
        [InlineData("{}")]
        [InlineData("{ \"readerScan\": true, \"interactive\": true }")]
        [InlineData("{ \"secureKeys\": false, \"readerScan\": true }")]
        [InlineData("{ \"secureKeys\": true, \"oops\": 1 }")]
        public async Task WithoutTheSetting_TheFixtureStartsNothing_AndEndsNothing_AndSaysWhy(string settingsFile)
        {
            var steps = new Steps();
            var fixture = steps.Fixture(HilSettings.Parse(settingsFile, Source));

            await fixture.InitializeAsync();
            await fixture.CleanupTestAidAsync();
            await fixture.DisposeAsync();

            Assert.Equal((0, 0), (steps.Starts, steps.Ends));
            Assert.False(fixture.Started);
            Assert.Null(fixture.Host);

            // A test that still got this far ends as a skip, and the skip names the file and the setting.
            var skip = Assert.ThrowsAny<Exception>(fixture.SkipIfNoHardware);
            Assert.Contains("$XunitDynamicSkip$", skip.Message, StringComparison.Ordinal);
            Assert.Contains("secureKeys", skip.Message, StringComparison.Ordinal);
        }

        [Fact]
        public async Task WithTheSetting_TheFixtureStartsTheReaderOnce_AndEndsItOnce()
        {
            var steps = new Steps();
            var fixture = steps.Fixture(HilSettings.Parse("{ \"secureKeys\": true }", Source));

            await fixture.InitializeAsync();

            Assert.Equal(1, steps.Starts);
            Assert.True(fixture.Started);

            await fixture.DisposeAsync();

            Assert.Equal(1, steps.Ends);
        }

        [Fact]
        public async Task TheSettingAloneIsEnough_NoReaderScanIsNeeded()
        {
            var steps = new Steps();
            var fixture = steps.Fixture(HilSettings.Parse("{ \"secureKeys\": true, \"readerScan\": false }", Source));

            await fixture.InitializeAsync();

            Assert.Equal(1, steps.Starts);
        }

        /// <summary>Counts the steps that would open and close the reader.</summary>
        private sealed class Steps
        {
            public int Starts { get; private set; }

            public int Ends { get; private set; }

            public HilSecureKeyFixture Fixture(HilSettings settings) => new HilSecureKeyFixture(
                settings,
                new HilFixture(),
                () =>
                {
                    Starts++;

                    return Task.CompletedTask;
                },
                () =>
                {
                    Ends++;

                    return Task.CompletedTask;
                });
        }
    }
}
