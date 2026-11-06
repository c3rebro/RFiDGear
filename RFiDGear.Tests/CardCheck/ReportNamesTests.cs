using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

using RFiDGear.ExtensionLibraries.CardCheck;

using Xunit;

namespace RFiDGear.Tests.CardCheck
{
    public sealed class ReportNamesTests
    {
        private static string Hash4(string original) =>
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(original))).Substring(0, 4).ToLowerInvariant();

        [Theory]
        [InlineData("KP-000005")]
        [InlineData("5")]
        [InlineData("A B")]
        [InlineData("job.1")]
        [InlineData("A~B")]
        [InlineData("A_B")]
        [InlineData("\u00C4\u00D6\u00DC-\u00DF")]
        [InlineData("A\u007fB")]
        [InlineData("A\u0085B")]
        [InlineData("COM10")]
        [InlineData("NUL-1")]
        [InlineData("CONSOLE")]
        [InlineData("AUX1")]
        public void ToSegment_ANameWindowsAllows_IsUsedExactlyAsItIs(string name)
        {
            var result = ReportNames.ToSegment(name, "job");

            Assert.True(result.IsSuccess);
            Assert.Equal(name, result.Value);
        }

        [Fact]
        public void ToSegment_TrimsBlanksAroundTheName()
        {
            Assert.Equal("123", ReportNames.ToSegment("  123\t", "job").Value);
        }

        [Theory]
        [InlineData("A/B")]
        [InlineData("A\\B")]
        [InlineData("A:B")]
        [InlineData("A*B")]
        [InlineData("A?B")]
        [InlineData("A\"B")]
        [InlineData("A<B")]
        [InlineData("A>B")]
        [InlineData("A|B")]
        [InlineData("A\tB")]
        [InlineData("A\u0001B")]
        public void ToSegment_ACharacterWindowsForbids_BecomesUnderscoreWithAHashOfTheOriginal(string name)
        {
            var result = ReportNames.ToSegment(name, "job");

            Assert.True(result.IsSuccess);
            Assert.Equal("A_B~" + Hash4(name), result.Value);
        }

        [Fact]
        public void ToSegment_TwoDifferentNumbersNeverShareAName()
        {
            var names = new[] { "A/B", "A?B", "A:B", "A_B", "A\\B", "A*B" }
                .Select(n => ReportNames.ToSegment(n, "job").Value)
                .ToList();

            Assert.Equal(names.Count, names.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("\t")]
        public void ToSegment_NothingToName_IsRefusedAsEmpty(string? name)
        {
            var result = ReportNames.ToSegment(name, "chip");

            Assert.False(result.IsSuccess);
            Assert.Equal(FailureKind.InvalidInput, result.Failure!.Kind);
            Assert.Equal("report.chip.empty", result.Failure.Code);
        }

        [Theory]
        [InlineData(".")]
        [InlineData("..")]
        [InlineData("abc.")]
        [InlineData("abc...")]
        [InlineData("NUL")]
        [InlineData("nul")]
        [InlineData("Con")]
        [InlineData("PRN")]
        [InlineData("AUX")]
        [InlineData("COM1")]
        [InlineData("com9")]
        [InlineData("LPT1")]
        [InlineData("lpt9")]
        [InlineData("NUL.txt")]
        [InlineData("CON.x.y")]
        [InlineData("NUL ")]
        public void ToSegment_ANameWindowsWouldMisreadOrRefuse_IsRefused(string name)
        {
            var result = ReportNames.ToSegment(name, "job");

            Assert.False(result.IsSuccess);
            Assert.Equal(FailureKind.InvalidInput, result.Failure!.Kind);
            Assert.Equal("report.job.invalid", result.Failure.Code);
        }

        [Fact]
        public void ToSegment_FailureTextNeverContainsTheValue()
        {
            var result = ReportNames.ToSegment("SECRET-CUSTOMER-NUL.", "job");
            var other = ReportNames.ToSegment("NUL", "job");

            Assert.DoesNotContain("SECRET", result.Failure!.SafeMessage, StringComparison.Ordinal);
            Assert.DoesNotContain("NUL", other.Failure!.SafeMessage, StringComparison.Ordinal);
        }

        [Theory]
        [InlineData("..\\..\\Windows\\System32")]
        [InlineData("../../etc/passwd")]
        [InlineData("C:\\Windows")]
        [InlineData("\\\\server\\share\\x")]
        [InlineData("a/../b")]
        [InlineData("a\\..\\..\\b")]
        [InlineData("..\\")]
        [InlineData("/")]
        [InlineData("\\")]
        [InlineData("x\0y")]
        [InlineData("CON\\x")]
        [InlineData("...\\...")]
        public void ToSegment_HostileInput_IsRefusedOrBecomesOneHarmlessSegment(string hostile)
        {
            var result = ReportNames.ToSegment(hostile, "job");

            if (!result.IsSuccess)
            {
                return;
            }

            var segment = result.Value;
            Assert.Equal(segment, Path.GetFileName(segment));
            Assert.Equal(-1, segment.IndexOfAny(Path.GetInvalidFileNameChars()));
            Assert.NotEqual(".", segment);
            Assert.NotEqual("..", segment);
            Assert.False(segment.EndsWith('.'));
        }
    }
}
