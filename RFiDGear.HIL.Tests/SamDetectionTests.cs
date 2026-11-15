#nullable enable
using RFiDGear.Services.Sam;

using System;
using System.Linq;
using System.Threading.Tasks;

using Elatec.NET.Sam;

using Xunit;
using Xunit.Sdk;

namespace RFiDGear.HIL.Tests
{
    /// <summary>
    /// The MIFARE SAM AV3 in a PC/SC reader, found and read through LibLogicalAccess (see <c>SAM-AV3.md</c>). Read-only: the case
    /// reads the ATR of the card in each reader, asks the SAM for its version (a command that needs no key) and sends one
    /// instruction that does not exist. It never authenticates, activates or writes. The activation and the key entry are for the
    /// person to try in the window, with the checklist.
    /// <para>
    /// It needs the reader-scan tests (<c>readerScan</c> in <c>hil.settings.json</c>) and does not depend on <c>readerKind</c>: the
    /// SAM sits in a PC/SC reader of its own, whatever reader the other cases use. Without a SAM in a PC/SC reader it is skipped.
    /// </para>
    /// </summary>
    [Collection("HILSam")]
    public sealed class SamDetectionTests
    {
        /// <summary>
        /// The SAM is found by the ATR alone, gives its hardware and software information, and answers an instruction that does not
        /// exist with a status word, which has to come through as an answer and not as an exception (the host protocol depends on it).
        /// </summary>
        /// <returns>The task of the test.</returns>
        [HilReaderFact]
        public Task ASamAv3InAPcscReader_IsFound_AndGivesItsVersion() =>
            StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var finder = new SamReaderFinder(new LlaSamReaderSource());
                var search = await Task.Run(() => finder.Find());

                if (search.EnumerationFailed)
                {
                    throw SkipException.ForSkip("The PC/SC readers could not be listed. Is the Smart Card service running?");
                }

                if (search.Readers.Count == 0)
                {
                    throw SkipException.ForSkip("No MIFARE SAM AV3 was found in a PC/SC reader. Put it into the contact slot of a reader and close the programs that use it.");
                }

                Assert.All(search.Readers, reader => Assert.True(SamDetection.IsSamAv3(reader.Atr), "a reader was found without the marker in its ATR"));

                using var connection = finder.Open(search.Readers[0].Name);

                Assert.NotNull(connection);

                var version = await SamDetection.ReadVersionAsync(connection!.Channel);

                Assert.NotNull(version);
                Assert.Equal(14, version!.HardwareInfo.Length);
                Assert.Equal(14, version.SoftwareInfo.Length);

                // An instruction that does not exist. The bench SAM answers 6D00; any status word but success is right here, and
                // that it is an answer and no exception is what the case is about.
                var unknown = await connection.Channel.TransmitAsync(new SamApdu(0x80, 0xFF, 0x00, 0x00, null, 0x00));

                Assert.NotEqual(0x9000, unknown.StatusWord);
            });
    }

    /// <summary>
    /// A key entry that is written to a real MIFARE SAM AV3 and read back (see <c>SAM-AV3.md</c>). It <b>writes to the SAM</b>, so it
    /// needs <c>cardWrites</c> as well as <c>readerScan</c>. It never activates a SAM and never touches the master key: it needs a SAM that
    /// is activated and still has the factory master key, and otherwise it is skipped.
    /// </summary>
    [Collection("HILSam")]
    public sealed class SamKeyEntryTests
    {
        // An entry that is out of the way of the ones a person uses (1 to 10). The key is a test key and no secret.
        private const byte Entry = 120;
        private const string TestKeyHex = "0F1E2D3C4B5A69788796A5B4C3D2E1F0";

        /// <summary>
        /// A blank entry of a SAM is disabled (SET bit 9, AN12701 table 2). The entry that the application writes must be read back
        /// enabled, an AES-128 key, and of the PICC key class: otherwise the key in it could not be used to authenticate a PICC.
        /// </summary>
        /// <returns>The task of the test.</returns>
        [HilCardWriteFact]
        public Task AKeyEntryThatIsWritten_IsReadBack_EnabledAndOfThePiccKeyClass() =>
            StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var finder = new SamReaderFinder(new LlaSamReaderSource());
                var search = await Task.Run(() => finder.Find());

                if (search.Readers.Count == 0)
                {
                    throw SkipException.ForSkip("No MIFARE SAM AV3 was found in a PC/SC reader.");
                }

                using var connection = finder.Open(search.Readers[0].Name);

                Assert.NotNull(connection);

                using var session = new Av3HostSession(connection!.Channel);

                try
                {
                    // Only the factory key: a SAM that was never activated says 6985, and a SAM with another master key refuses.
                    await session.AuthenticateHostAsync(0, 0, new byte[16]);
                }
                catch (SamException e)
                {
                    throw SkipException.ForSkip("The host authentication with the factory master key failed (status " + e.StatusWord.ToString("X4")
                        + "): the SAM is not activated, or its master key is not the factory key. This case does neither activate nor change a master key.");
                }

                var before = await session.ReadKeyEntryAsync(Entry);

                Assert.NotNull(before);

                // A blank entry is disabled. An entry that is enabled is in use, unless it is what this case wrote on an earlier run.
                if (before!.IsEnabled && before.Verdict != SamKeyEntryCheck.Matches)
                {
                    throw SkipException.ForSkip("Key entry " + Entry + " is in use (enabled, and not an AES-128 PICC key as this case writes it): it is left alone.");
                }

                var key = Convert.FromHexString(TestKeyHex);

                try
                {
                    await session.ChangeKeyEntryAsync(Entry, SamAv3KeyEntry.CreateAes128PiccKey(key, 0));
                }
                finally
                {
                    Array.Clear(key, 0, key.Length);
                }

                var after = await session.ReadKeyEntryAsync(Entry);

                Assert.NotNull(after);
                Assert.Equal(0, after!.Set & 0x0200);
                Assert.True(after.IsEnabled);
                Assert.True(after.IsAes128);
                Assert.Equal(1, after.KeyClass);
                Assert.Equal(SamKeyEntryCheck.Matches, after.Verdict);
                Assert.Equal(0xFF, after.RefNoKuc);
                Assert.Equal(0, after.VersionA);
            });
    }

    /// <summary>The collection of the SAM case: it uses the PC/SC readers, so nothing else runs at the same time.</summary>
    [CollectionDefinition("HILSam", DisableParallelization = true)]
    public sealed class HilSamCollection { }
}
