using System;
using System.Collections.Generic;
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
        private static string Hash8(string original) =>
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(original))).Substring(0, 8).ToLowerInvariant();

        [Theory]
        [InlineData("KP-000005")]
        [InlineData("5")]
        [InlineData("A B")]
        [InlineData("job.1")]
        [InlineData("A_B")]
        [InlineData("\u00C4\u00D6\u00DC-\u00DF")]
        [InlineData("A\u007fB")]
        [InlineData("A\u0085B")]
        [InlineData("COM\u2074")]
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
        [InlineData("A~B")]
        public void ToSegment_ACharacterThatIsNotAllowed_BecomesUnderscoreWithAHashOfTheOriginal(string name)
        {
            var result = ReportNames.ToSegment(name, "job");

            Assert.True(result.IsSuccess);
            Assert.Equal("A_B~" + Hash8(name), result.Value);
        }

        [Fact]
        public void ToSegment_TwoDifferentNumbersNeverShareAName()
        {
            var names = new[] { "A/B", "A?B", "A:B", "A_B", "A\\B", "A*B", "A~B" }
                .Select(n => ReportNames.ToSegment(n, "job").Value)
                .ToList();

            Assert.Equal(names.Count, names.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        }

        [Fact]
        public void ToSegment_ALiteralNameThatLooksLikeAChangedOne_IsChangedItself_SoItCannotAliasTheOriginal()
        {
            var changed = ReportNames.ToSegment("A/B", "job").Value;

            var lookalike = ReportNames.ToSegment(changed, "job").Value;

            Assert.NotEqual(changed, lookalike);
            Assert.Contains(ReportNames.EscapeMarker, lookalike);
        }

        [Fact]
        public void ToSegment_DifferentCharactersThatCollapseToTheSameSkeleton_GetDifferentHashes()
        {
            var one = ReportNames.ToSegment("A///??*?B", "job").Value;
            var two = ReportNames.ToSegment("A///*><*B", "job").Value;

            Assert.Equal(one.Substring(0, one.IndexOf(ReportNames.EscapeMarker)), two.Substring(0, two.IndexOf(ReportNames.EscapeMarker)));
            Assert.NotEqual(one, two);
        }

        [Fact]
        public void ToSegment_NoTwoDifferentNumbersOfAnAlphabetOfTroublemakersShareAName()
        {
            // Every string of up to five characters from an alphabet of allowed, forbidden and look-alike characters.
            var alphabet = new[] { 'a', '/', '?', '~', '_', '-' };
            var seen = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var inputs = new List<string> { string.Empty };
            var count = 0;

            for (var length = 1; length <= 5; length++)
            {
                inputs = inputs.SelectMany(prefix => alphabet.Select(c => prefix + c)).ToList();

                foreach (var original in inputs)
                {
                    var name = ReportNames.ToSegment(original, "job").Value;
                    count++;

                    Assert.False(seen.TryGetValue(name, out var other), $"'{original}' and '{other}' both became '{name}'");
                    seen[name] = original;
                }
            }

            Assert.Equal(alphabet.Length + 36 + 216 + 1296 + 7776, count);
        }

        [Fact]
        public void ToSegment_TheSeparatorOfJobAndChipIsNotAllowedInAChipNumber_ButIsInAJobNumber()
        {
            Assert.Equal("KP-000005", ReportNames.ToSegment("KP-000005", "job").Value);

            var chip = ReportNames.ToSegment("B-C", "chip", alsoNotAllowed: ReportFiles.JobChipSeparator).Value;

            Assert.Equal("B_C~" + Hash8("B-C"), chip);
            Assert.DoesNotContain('-', chip);
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
        [InlineData("COM0")]
        [InlineData("lpt0")]
        [InlineData("COM¹")]
        [InlineData("com²")]
        [InlineData("COM³")]
        [InlineData("LPT¹")]
        [InlineData("lpt²")]
        [InlineData("LPT³.txt")]
        [InlineData("COM¹.txt")]
        [InlineData("CONIN$")]
        [InlineData("conout$")]
        [InlineData("CONIN$.x")]
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
