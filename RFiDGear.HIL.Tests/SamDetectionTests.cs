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

    /// <summary>The collection of the SAM case: it uses the PC/SC readers, so nothing else runs at the same time.</summary>
    [CollectionDefinition("HILSam", DisableParallelization = true)]
    public sealed class HilSamCollection { }
}
