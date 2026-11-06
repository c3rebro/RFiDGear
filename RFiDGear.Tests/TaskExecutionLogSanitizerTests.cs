using System;

using RFiDGear.Services.TaskExecution;

using Xunit;

namespace RFiDGear.Tests
{
    /// <summary>
    /// What the log filter removes and, as importantly, what it leaves alone. Exception messages from card
    /// operations can contain keys; the filter is the last thing between them and the log file.
    /// </summary>
    public class TaskExecutionLogSanitizerTests
    {
        // ── labels followed by ':' or '=' ──

        [Theory]
        [InlineData("Key=0011223344556677", "0011223344556677")]
        [InlineData("key: 0011223344556677", "0011223344556677")]
        [InlineData("MasterKey=SECRETVALUE", "SECRETVALUE")]
        [InlineData("PiccKey=SECRETVALUE", "SECRETVALUE")]
        [InlineData("appkey = SECRETVALUE", "SECRETVALUE")]
        [InlineData("ReadKey:SECRETVALUE", "SECRETVALUE")]
        [InlineData("WriteKey=SECRETVALUE", "SECRETVALUE")]
        [InlineData("payload=SECRETVALUE", "SECRETVALUE")]
        // labels that used to slip through
        [InlineData("KeyA=SECRETVALUE", "SECRETVALUE")]
        [InlineData("keyB: SECRETVALUE", "SECRETVALUE")]
        [InlineData("CardKey2=SECRETVALUE", "SECRETVALUE")]
        [InlineData("password=SECRETVALUE", "SECRETVALUE")]
        [InlineData("Password: SECRETVALUE", "SECRETVALUE")]
        [InlineData("passwd=SECRETVALUE", "SECRETVALUE")]
        [InlineData("pwd=SECRETVALUE", "SECRETVALUE")]
        [InlineData("passphrase=SECRETVALUE", "SECRETVALUE")]
        [InlineData("secret=SECRETVALUE", "SECRETVALUE")]
        [InlineData("api_token: SECRETVALUE", "SECRETVALUE")]
        [InlineData("Credentials=SECRETVALUE", "SECRETVALUE")]
        public void Sanitize_RedactsTheValueAfterASecretLabel(string text, string secret)
        {
            var result = TaskExecutionLogSanitizer.Sanitize($"Authentication failed. {text} (sector 3)");

            Assert.DoesNotContain(secret, result, StringComparison.Ordinal);
            Assert.Contains("[REDACTED]", result, StringComparison.Ordinal);
            Assert.Contains("Authentication failed.", result, StringComparison.Ordinal);
            Assert.Contains("(sector 3)", result, StringComparison.Ordinal);
        }

        [Theory]
        [InlineData("password=\"my secret phrase\" after", "phrase")]
        [InlineData("password='my secret phrase' after", "phrase")]
        public void Sanitize_RedactsAQuotedValueAsAWhole(string text, string secretWord)
        {
            var result = TaskExecutionLogSanitizer.Sanitize(text);

            Assert.DoesNotContain(secretWord, result, StringComparison.Ordinal);
            Assert.EndsWith(" after", result, StringComparison.Ordinal);
        }

        [Theory]
        [InlineData("password=\"ab\\\"cd ef\" after")]       // a backslash-escaped quote inside the value
        [InlineData("key=\"abc\"def after")]                  // text glued to the closing quote belongs to the value
        [InlineData("key='abc'def after")]
        public void Sanitize_RedactsAQuotedValueWithEdgesAsAWhole(string text)
        {
            Assert.Equal("[REDACTED] after", TaskExecutionLogSanitizer.Sanitize(text));
        }

        [Fact]
        public void Sanitize_RedactsAJsonStyleLabelAndKeepsTheRestOfTheDocument()
        {
            var result = TaskExecutionLogSanitizer.Sanitize("{\"password\":\"hunter2\",\"user\":\"bob\"}");

            Assert.DoesNotContain("hunter2", result, StringComparison.Ordinal);
            Assert.Contains("\"user\":\"bob\"}", result, StringComparison.Ordinal);
        }

        [Theory]
        [InlineData("Authentication failed for key: FF FF FF FF FF FF", "Authentication failed for [REDACTED]")]
        [InlineData("Key=A0 A1 A2 A3 A4 A5 (sector 3)", "[REDACTED] (sector 3)")]
        [InlineData("KeyA = A0, A1, A2, A3, A4, A5", "[REDACTED]")]
        [InlineData("Key=0xA0, 0xA1, 0xA2, 0xA3, 0xA4, 0xA5", "[REDACTED]")]
        [InlineData("password=FF FF FF FF FF FF", "[REDACTED]")]
        [InlineData("Key: [FF FF FF FF FF FF]", "[REDACTED]")]
        public void Sanitize_RedactsAWholeLabelledKeyWrittenAsSeparatedBytes(string text, string expected)
        {
            // The label's own value stops at the first space or comma. If the label rule ran before the hex rules,
            // this would remove one byte and leave the other five in the log.
            Assert.Equal(expected, TaskExecutionLogSanitizer.Sanitize(text));
        }

        [Fact]
        public void Sanitize_RedactsEveryLabelInOneMessage()
        {
            var result = TaskExecutionLogSanitizer.Sanitize("KeyA=AAAA1111, KeyB=BBBB2222; password=CCCC3333");

            Assert.DoesNotContain("AAAA1111", result, StringComparison.Ordinal);
            Assert.DoesNotContain("BBBB2222", result, StringComparison.Ordinal);
            Assert.DoesNotContain("CCCC3333", result, StringComparison.Ordinal);
        }

        // ── hexadecimal runs of key length ──

        [Theory]
        [InlineData("FFFFFFFFFFFF")]                                     // 6 bytes: MIFARE Classic key
        [InlineData("A0A1A2A3A4A5")]
        [InlineData("0123456789abcdef")]                                 // 8 bytes: DES key
        [InlineData("00112233445566778899AABBCCDDEEFF")]                 // 16 bytes: AES / 2K3DES key
        [InlineData("00112233445566778899AABBCCDDEEFF0011223344556677")] // 24 bytes: 3K3DES key
        public void Sanitize_RedactsHexRunsOfKeyLength(string hex)
        {
            var result = TaskExecutionLogSanitizer.Sanitize($"Failed to authenticate with {hex} on the card");

            Assert.DoesNotContain(hex, result, StringComparison.OrdinalIgnoreCase);
            Assert.Equal("Failed to authenticate with [REDACTED_HEX] on the card", result);
        }

        [Theory]
        [InlineData("04A1B2C3")]                    // 4-byte UID
        [InlineData("04A1B2C3D4E5F6")]              // 7-byte UID
        [InlineData("04A1B2C3D4E5F60718A1")]        // 10-byte UID
        [InlineData("00112233445")]                 // 11 digits
        [InlineData("0011223344556")]               // 13 digits
        [InlineData("001122334455667788990011")]    // 24 digits (12 bytes): not a key size
        public void Sanitize_LeavesHexRunsOfOtherLengthsAlone(string hex)
        {
            var text = $"Card UID {hex} was removed";

            Assert.Equal(text, TaskExecutionLogSanitizer.Sanitize(text));
        }

        [Fact]
        public void Sanitize_DoesNotCutAKeyLengthRunOutOfALongerWord()
        {
            // A 12-digit stretch inside a longer hex run is not a key, and cutting it out would only mangle the text.
            const string text = "trace id 0123456789ABCDEF0123456789ABCDEF01 end";

            Assert.Equal(text, TaskExecutionLogSanitizer.Sanitize(text));
        }

        // ── bytes written with separators ──

        [Theory]
        [InlineData("FF FF FF FF FF FF")]
        [InlineData("FF:FF:FF:FF:FF:FF")]
        [InlineData("a0-a1-a2-a3-a4-a5")]
        [InlineData("0xA0, 0xA1, 0xA2, 0xA3, 0xA4, 0xA5")]
        [InlineData("00 11 22 33 44 55 66 77 88 99 AA BB CC DD EE FF")]
        public void Sanitize_RedactsSixOrMoreSeparatedHexBytes(string bytes)
        {
            var result = TaskExecutionLogSanitizer.Sanitize($"key bytes were {bytes} when it failed");

            Assert.Equal("key bytes were [REDACTED_HEX] when it failed", result);
        }

        [Theory]
        [InlineData("FF FF FF FF FF")]              // five bytes
        [InlineData("12:30:45")]
        [InlineData("2026-11-06 10:15:30")]
        public void Sanitize_LeavesShortByteSequencesAndTimestampsAlone(string text)
        {
            var message = $"at {text} the reader stopped";

            Assert.Equal(message, TaskExecutionLogSanitizer.Sanitize(message));
        }

        // ── ordinary text, and the limits ──

        [Fact]
        public void Sanitize_LeavesOrdinaryMessagesUnchanged()
        {
            const string text = "Sector 3 authentication failed with AuthFailure at task 12 (retry 2 of 3)";

            Assert.Equal(text, TaskExecutionLogSanitizer.Sanitize(text));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Sanitize_ReturnsNullEmptyAndBlankTextAsTheyAre(string? text)
        {
            Assert.Equal(text, TaskExecutionLogSanitizer.Sanitize(text));
        }

        [Fact]
        public void Sanitize_CutsTheRedactedTextAtTheLimit()
        {
            var result = TaskExecutionLogSanitizer.Sanitize(new string('x', 1000));

            Assert.Equal(TaskExecutionLogSanitizer.MaxLength, result.Length);
            Assert.Equal(256, TaskExecutionLogSanitizer.MaxLength);
        }

        [Fact]
        public void Sanitize_RedactsBeforeCutting_SoAKeyThatStraddlesTheLimitLeavesNoDigitsBehind()
        {
            // A 32-digit key starts a few characters before the limit. Cutting first would keep its first five
            // digits, too few for any rule to recognise; redacting first replaces the whole run and only then cuts.
            var text = new string('x', 250) + " 00112233445566778899AABBCCDDEEFF tail";

            var result = TaskExecutionLogSanitizer.Sanitize(text);

            Assert.DoesNotContain("0011", result, StringComparison.Ordinal);
            Assert.StartsWith(new string('x', 250), result, StringComparison.Ordinal);
            Assert.Equal(TaskExecutionLogSanitizer.MaxLength, result.Length);
        }

        // ── what the filter does not recognise, pinned so the documentation stays true ──

        [Theory]
        [InlineData("AQIDBAUGBwgJCgsMDQ4PEA==")]         // a Base64 key with no label
        [InlineData("255 255 255 255 255 255")]           // six bytes in decimal
        [InlineData("hunter2")]                           // a bare secret with no label
        [InlineData("FFFF FFFF FFFF")]                    // a 6-byte key written in groups of two bytes
        [InlineData("00112233 44556677 8899AABB CCDDEEFF")] // a 16-byte key written in groups of four bytes
        [InlineData("password is hunter2")]               // a label not followed by ':' or '='
        [InlineData("key 'hunter2'")]
        [InlineData("<Password>hunter2</Password>")]      // an XML element
        public void Sanitize_DoesNotRecogniseSecretsWithNeitherALabelNorAKeyLikeShape(string text)
        {
            var message = $"value was {text}";

            Assert.Equal(message, TaskExecutionLogSanitizer.Sanitize(message));
        }

        // ── harmless text of the same shape that is removed too, as documented ──

        [Theory]
        [InlineData("guid 3f2504e0-4f89-11d3-9a0c-0305e82c3301 end", "guid 3f2504e0-4f89-11d3-9a0c-[REDACTED_HEX] end")]
        [InlineData("mac 00:1A:2B:3C:4D:5E end", "mac [REDACTED_HEX] end")]
        [InlineData("counts 10 20 30 40 50 60 end", "counts [REDACTED_HEX] end")]
        [InlineData("keyboard=abc end", "[REDACTED] end")]
        public void Sanitize_AlsoRemovesHarmlessTextOfTheSameShape(string text, string expected)
        {
            Assert.Equal(expected, TaskExecutionLogSanitizer.Sanitize(text));
        }
    }
}
