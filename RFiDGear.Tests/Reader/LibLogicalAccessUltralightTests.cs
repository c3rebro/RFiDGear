using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using LibLogicalAccess;
using RFiDGear.Infrastructure;
using RFiDGear.Infrastructure.ReaderProviders;
using RFiDGear.Infrastructure.Ultralight;
using Xunit;

namespace RFiDGear.Tests.Reader
{
    /// <summary>
    /// The page operations of the PC/SC provider, against a reader in memory. LibLogicalAccess knows the plain Ultralight and the
    /// Ultralight C and no other chip of the family (an NTAG215 on the bench is a chip of an unsupported type to it), so those two are
    /// what is read, written and authenticated; every other chip and the password of an EV1 and an NTAG are a protocol constraint, and
    /// such a call touches no card. What each test pins is a promise of the one-attempt rule: a refusal ends the call and is never
    /// repeated, a short answer is never a success, and the key array is cleared.
    /// </summary>
    public class LibLogicalAccessUltralightTests
    {
        private static readonly byte[] CKey = Convert.FromHexString("00112233445566778899AABBCCDDEEFF");

        private const string CKeyHex = "00112233445566778899AABBCCDDEEFF";

        private static UltralightAccess Plain() => UltralightAccess.Open(UltralightExpectation.Plain);

        // ── reading ──

        [Fact]
        public async Task APlainUltralight_IsRead_OnePageAtATime()
        {
            var reader = new UltralightPcscTestReader(UltralightVariant.Plain);

            var result = await reader.ReadMifareUltralightPagesAsync(Plain(), 4, 4);

            Assert.True(result.Succeeded);
            Assert.Equal(UltralightVariant.Plain, result.Variant);
            Assert.Equal(4, result.PagesDone);
            Assert.Equal(reader.PagesOf(4, 4), result.Data);
            Assert.Equal(new[] { "select", "read 4", "read 5", "read 6", "read 7" }, reader.Calls);
        }

        [Fact]
        public async Task WithoutANamedChip_TheChipThatIsFound_IsTheOneThatIsUsed()
        {
            var reader = new UltralightPcscTestReader(UltralightVariant.C);

            var result = await reader.ReadMifareUltralightPagesAsync(UltralightAccess.Open(), 40, 2);

            Assert.True(result.Succeeded);
            Assert.Equal(UltralightVariant.C, result.Variant);
            Assert.Equal(reader.PagesOf(40, 2), result.Data);
        }

        [Theory]
        [InlineData(42, 4)]
        [InlineData(43, 2)]
        [InlineData(44, 1)]
        [InlineData(0, 48)]
        public async Task AnUltralightC_IsNotReadIntoItsKeyPages(int start, int count)
        {
            var named = new UltralightPcscTestReader(UltralightVariant.C);
            var found = new UltralightPcscTestReader(UltralightVariant.C);

            var byName = await named.ReadMifareUltralightPagesAsync(UltralightAccess.Open(UltralightExpectation.C), start, count);
            var byTheChip = await found.ReadMifareUltralightPagesAsync(UltralightAccess.Open(), start, count);

            Assert.Equal(ERROR.ProtocolConstraint, byName.Error);
            Assert.Empty(named.Calls);
            Assert.Equal(ERROR.ProtocolConstraint, byTheChip.Error);
            Assert.Equal(UltralightVariant.C, byTheChip.Variant);
            Assert.Equal(0, found.CountStartingWith("read"));
        }

        [Fact]
        public async Task APlainUltralight_HasNoPage16_AndNothingIsReadToFindThatOut()
        {
            var reader = new UltralightPcscTestReader(UltralightVariant.Plain);

            var result = await reader.ReadMifareUltralightPagesAsync(UltralightAccess.Open(), 15, 2);

            Assert.Equal(ERROR.ProtocolConstraint, result.Error);
            Assert.Equal(UltralightVariant.Plain, result.Variant);
            Assert.Equal(new[] { "select" }, reader.Calls);
        }

        [Fact]
        public async Task AnotherChipThanTheTaskExpects_IsRefused_AndNothingIsRead()
        {
            var reader = new UltralightPcscTestReader(UltralightVariant.Plain);

            var result = await reader.ReadMifareUltralightPagesAsync(UltralightAccess.Open(UltralightExpectation.C), 4, 1);

            Assert.Equal(ERROR.ProtocolConstraint, result.Error);
            Assert.Equal(UltralightVariant.Plain, result.Variant);
            Assert.Equal(new[] { "select" }, reader.Calls);
        }

        // ── what LibLogicalAccess does not know, and the password ──

        [Fact]
        public async Task AChipThatLibLogicalAccessDoesNotKnow_IsAProtocolConstraint_AndNothingIsRead()
        {
            // An NTAG215 or an Ultralight EV1 is "__UNSUPPORTED__UNKNOWN" to it, and so is any chip of another family.
            var reader = new UltralightPcscTestReader(UltralightVariant.Ntag215);

            var result = await reader.ReadMifareUltralightPagesAsync(UltralightAccess.Open(), 4, 1);

            Assert.Equal(ERROR.ProtocolConstraint, result.Error);
            Assert.Null(result.Variant);
            Assert.Equal(new[] { "select" }, reader.Calls);
        }

        [Theory]
        [InlineData(UltralightExpectation.Ev1Mf0ul11)]
        [InlineData(UltralightExpectation.Ev1Mf0ul21)]
        [InlineData(UltralightExpectation.Ntag213)]
        [InlineData(UltralightExpectation.Ntag215)]
        [InlineData(UltralightExpectation.Ntag216)]
        public async Task ATaskThatExpectsAnEv1OrAnNtag_MakesNoCallToThePcscCard(UltralightExpectation expected)
        {
            var reader = new UltralightPcscTestReader(UltralightVariant.Ntag215);

            Assert.Equal(ERROR.ProtocolConstraint, (await reader.ReadMifareUltralightPagesAsync(UltralightAccess.Open(expected), 4, 1)).Error);
            Assert.Equal(ERROR.ProtocolConstraint, (await reader.WriteMifareUltralightPagesAsync(UltralightAccess.Open(expected), 4, new byte[4])).Error);
            Assert.Empty(reader.Calls);
        }

        [Theory]
        [InlineData(UltralightExpectation.Auto, UltralightAuth.None, true)]
        [InlineData(UltralightExpectation.Plain, UltralightAuth.None, true)]
        [InlineData(UltralightExpectation.C, UltralightAuth.None, true)]
        [InlineData(UltralightExpectation.C, UltralightAuth.UltralightC, true)]
        [InlineData(UltralightExpectation.Ev1Mf0ul11, UltralightAuth.None, false)]
        [InlineData(UltralightExpectation.Ev1Mf0ul21, UltralightAuth.None, false)]
        [InlineData(UltralightExpectation.Ntag213, UltralightAuth.None, false)]
        [InlineData(UltralightExpectation.Ntag215, UltralightAuth.None, false)]
        [InlineData(UltralightExpectation.Ntag216, UltralightAuth.None, false)]
        [InlineData(UltralightExpectation.Ntag215, UltralightAuth.Password, false)]
        [InlineData(UltralightExpectation.Auto, UltralightAuth.Password, false)]
        public void TheReader_SaysWithoutACard_WhatItCanDoAtAll(UltralightExpectation expected, UltralightAuth auth, bool supported)
        {
            // The same answer that the calls give below, so that a task can ask before it asks a provider for a key.
            var reader = new UltralightPcscTestReader(UltralightVariant.Ntag215);

            Assert.Equal(supported, reader.SupportsUltralightAccess(new UltralightAccess(expected, auth)));
            Assert.Empty(reader.Calls);
        }

        [Fact]
        public async Task ThePasswordOfAnEv1OrAnNtag_IsNotSentByPcsc_AndMakesNoCall()
        {
            // The password authentication is a command of the Elatec reader; LibLogicalAccess has no such command.
            var reader = new UltralightPcscTestReader(UltralightVariant.Ntag215);
            var access = new UltralightAccess(UltralightExpectation.Ntag215, UltralightAuth.Password, "A1B2C3D4", "5566");

            Assert.Equal(ERROR.ProtocolConstraint, (await reader.AuthenticateMifareUltralightAsync(access)).Error);
            Assert.Equal(ERROR.ProtocolConstraint, (await reader.ReadMifareUltralightPagesAsync(access, 4, 1)).Error);
            Assert.Empty(reader.Calls);
            Assert.Empty(reader.SecretsReceived);
        }

        [Fact]
        public async Task WithoutAChip_TheCallIsATransportError_NotASuccessThatReadNothing()
        {
            var reader = new UltralightPcscTestReader(UltralightVariant.Plain) { ChipOnReader = false };

            var result = await reader.ReadMifareUltralightPagesAsync(Plain(), 4, 1);

            Assert.Equal(ERROR.TransportError, result.Error);
            Assert.Empty(result.Data);
            Assert.Equal(new[] { "select" }, reader.Calls);
        }

        [Fact]
        public async Task APcscProviderWithoutAReader_AnswersTransportError_ForEveryPageOperation()
        {
            // The provider as the application creates it before a reader is chosen: no chip can be found, so every call ends
            // TransportError, and none throws. (The base class answers ProtocolConstraint to a reader that does not override the page
            // operations; both providers override them now, so no reader is left to test that default with.)
            var reader = new LibLogicalAccessProvider();

            Assert.Equal(ERROR.TransportError, (await reader.ReadMifareUltralightPagesAsync(UltralightAccess.Open(), 4, 1)).Error);
            Assert.Equal(ERROR.TransportError, (await reader.WriteMifareUltralightPagesAsync(UltralightAccess.Open(), 4, new byte[4])).Error);
            Assert.Equal(ERROR.TransportError, (await reader.AuthenticateMifareUltralightAsync(new UltralightAccess(UltralightExpectation.C, UltralightAuth.UltralightC, CKeyHex))).Error);
        }

        // ── refusals and faults ──

        [Fact]
        public async Task ARefusedRead_EndsTheCall_WithThePagesBeforeIt_AndIsNeverRepeated()
        {
            var reader = new UltralightPcscTestReader(UltralightVariant.Plain);
            reader.RefusedReads.Add(6);

            var result = await reader.ReadMifareUltralightPagesAsync(Plain(), 4, 4);

            Assert.Equal(ERROR.PermissionDenied, result.Error);
            Assert.Equal(2, result.PagesDone);
            Assert.Equal(6, result.FailedPage);
            Assert.Equal(reader.PagesOf(4, 2), result.Data);
            Assert.Equal(new[] { "select", "read 4", "read 5", "read 6" }, reader.Calls);
        }

        [Fact]
        public async Task AnEmptyAnswer_IsNeverASuccess()
        {
            // The old code answered NoError whether or not a page was read.
            var reader = new UltralightPcscTestReader(UltralightVariant.Plain);
            reader.EmptyReads.Add(5);

            var result = await reader.ReadMifareUltralightPagesAsync(Plain(), 4, 3);

            Assert.Equal(ERROR.Unknown, result.Error);
            Assert.Equal(1, result.PagesDone);
            Assert.Equal(5, result.FailedPage);
            Assert.Equal(reader.PagesOf(4, 1), result.Data);
        }

        [Fact]
        public async Task AFaultOfTheTransport_DuringTheSelection_IsATransportError_AndNotRepeated()
        {
            var reader = new UltralightPcscTestReader(UltralightVariant.Plain) { SelectThrows = new IOException("SCardTransmit failed A1B2C3D4") };

            var result = await reader.ReadMifareUltralightPagesAsync(Plain(), 4, 1);

            Assert.Equal(ERROR.TransportError, result.Error);
            Assert.Equal(new[] { "select" }, reader.Calls);
        }

        [Fact]
        public async Task AnUnexpectedFault_IsUnknown_AndTheReaderCanBeUsedAgainAfterIt()
        {
            var reader = new UltralightPcscTestReader(UltralightVariant.Plain) { SelectThrows = new InvalidOperationException("boom") };

            Assert.Equal(ERROR.Unknown, (await reader.ReadMifareUltralightPagesAsync(Plain(), 4, 1)).Error);

            reader.SelectThrows = null;

            Assert.True((await reader.ReadMifareUltralightPagesAsync(Plain(), 4, 1)).Succeeded);
        }

        [Fact]
        public async Task ACancellation_IsNotSwallowed_AndTheReaderCanBeUsedAgainAfterIt()
        {
            var reader = new UltralightPcscTestReader(UltralightVariant.Plain) { SelectThrows = new OperationCanceledException() };

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reader.ReadMifareUltralightPagesAsync(Plain(), 4, 1));

            // The gate of the session was released: a call that waited for it for ever would end this test by its timeout.
            reader.SelectThrows = null;
            var again = reader.ReadMifareUltralightPagesAsync(Plain(), 4, 1);

            Assert.Same(again, await Task.WhenAny(again, Task.Delay(TimeSpan.FromSeconds(10))));
            Assert.True((await again).Succeeded);
        }

        [Fact]
        public async Task TwoCalls_AreNeverInTheReaderAtTheSameTime()
        {
            // One chip handle and one session: a second call that selected while the first was reading would send its commands through
            // the selection of the first (and the C key of the one would be the other's).
            var reader = new UltralightPcscTestReader(UltralightVariant.Plain) { HoldReads = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously) };

            var first = reader.ReadMifareUltralightPagesAsync(Plain(), 4, 1);
            await WaitUntilAsync(() => reader.Calls.Contains("read 4"));

            var second = reader.ReadMifareUltralightPagesAsync(Plain(), 8, 1);
            await Task.Delay(200);

            // The second call has not selected yet: it waits for the first.
            Assert.Equal(new[] { "select", "read 4" }, reader.Calls.ToArray());
            Assert.False(second.IsCompleted);

            reader.HoldReads.SetResult(true);
            var both = await Task.WhenAll(first, second);

            Assert.All(both, result => Assert.True(result.Succeeded));
            Assert.Equal(new[] { "select", "read 4", "select", "read 8" }, reader.Calls);
        }

        [Fact]
        public async Task AWritePayload_IsFixedWhenTheCallIsMade_NotWhenTheReaderGetsToIt()
        {
            // The call waits for the session, and the caller may reuse its buffer meanwhile; what is written, and what the read-back is
            // compared with, is what the buffer held when the call was made.
            var reader = new UltralightPcscTestReader(UltralightVariant.Plain) { HoldReads = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously) };
            var first = reader.ReadMifareUltralightPagesAsync(Plain(), 4, 1);
            await WaitUntilAsync(() => reader.Calls.Contains("read 4"));

            var payload = Convert.FromHexString("A1B2C3D4");
            var write = reader.WriteMifareUltralightPagesAsync(Plain(), 8, payload);
            Array.Clear(payload, 0, payload.Length);

            reader.HoldReads.SetResult(true);
            await first;
            var result = await write;

            Assert.True(result.Succeeded);
            Assert.Equal(Convert.FromHexString("A1B2C3D4"), reader.PagesOf(8, 1));
        }

        private static async Task WaitUntilAsync(Func<bool> condition)
        {
            var until = DateTime.UtcNow.AddSeconds(10);

            while (!condition() && DateTime.UtcNow < until)
            {
                await Task.Delay(10);
            }

            Assert.True(condition(), "The first call never reached the reader.");
        }

        // ── writing ──

        [Fact]
        public async Task AWriteOfUserPages_IsWritten_AndReadBackToCheckIt()
        {
            var reader = new UltralightPcscTestReader(UltralightVariant.Plain);
            var data = Convert.FromHexString("A1B2C3D401020304");

            var result = await reader.WriteMifareUltralightPagesAsync(Plain(), 8, data);

            Assert.True(result.Succeeded);
            Assert.Equal(2, result.PagesDone);
            Assert.Equal(data, reader.PagesOf(8, 2));
            Assert.Equal(new[] { "select", "write 8", "write 9", "read 8", "read 9" }, reader.Calls);
        }

        [Fact]
        public async Task AProtectedPage_IsNotWritten_WithoutTheOptIn_AndNothingIsAskedOfTheCard()
        {
            var reader = new UltralightPcscTestReader(UltralightVariant.C);
            var before = (byte[])reader.Memory.Clone();

            var result = await reader.WriteMifareUltralightPagesAsync(UltralightAccess.Open(UltralightExpectation.C), 40, new byte[4]);

            Assert.Equal(ERROR.ProtocolConstraint, result.Error);
            Assert.Empty(reader.Calls);
            Assert.Equal(before, reader.Memory);
        }

        [Fact]
        public async Task APageThatTheChipFoundDoesNotHave_IsNotWritten_WhenNoChipWasNamed()
        {
            // With no chip named, only the chip that answers can say that page 16 is not there (a plain Ultralight has 16 pages).
            var reader = new UltralightPcscTestReader(UltralightVariant.Plain);

            var result = await reader.WriteMifareUltralightPagesAsync(UltralightAccess.Open(), 16, new byte[4]);

            Assert.Equal(ERROR.ProtocolConstraint, result.Error);
            Assert.Equal(new[] { "select" }, reader.Calls);
        }

        [Fact]
        public async Task TheKeyOfAnUltralightC_IsWritten_WithTheOptIn_AndNotReadBack()
        {
            // The key (pages 44-47) is write-only: it cannot be read back, and a write to it is as sure as the answer of the chip.
            var reader = new UltralightPcscTestReader(UltralightVariant.C);
            var access = new UltralightAccess(UltralightExpectation.C, allowProtectedPages: true);

            var result = await reader.WriteMifareUltralightPagesAsync(access, 44, CKey);

            Assert.True(result.Succeeded);
            Assert.Equal(4, result.PagesDone);
            Assert.Equal(CKey, reader.PagesOf(44, 4));
            Assert.Equal(0, reader.CountStartingWith("read"));
        }

        [Fact]
        public async Task ARefusedWrite_EndsTheCall_WithTheNumberOfPagesWritten_AndNothingIsWrittenAgain()
        {
            var reader = new UltralightPcscTestReader(UltralightVariant.Plain);
            reader.RefusedWrites.Add(9);

            var result = await reader.WriteMifareUltralightPagesAsync(Plain(), 8, new byte[12]);

            Assert.Equal(ERROR.PermissionDenied, result.Error);
            Assert.Equal(1, result.PagesDone);
            Assert.Equal(9, result.FailedPage);
            Assert.Equal(new[] { "select", "write 8", "write 9" }, reader.Calls);
        }

        [Fact]
        public async Task TheBytesOfEveryPageThatWasWritten_AreClearedAfterTheWrite_WhetherItWasKeptOrRefused()
        {
            // The pages of a write are copies of the payload, and the payload can be the key of an Ultralight C.
            var data = Convert.FromHexString("A1B2C3D401020304");
            var kept = new UltralightPcscTestReader(UltralightVariant.Plain);
            var refused = new UltralightPcscTestReader(UltralightVariant.Plain);
            refused.RefusedWrites.Add(9);

            await kept.WriteMifareUltralightPagesAsync(Plain(), 8, data);
            await refused.WriteMifareUltralightPagesAsync(Plain(), 8, data);

            Assert.Equal(2, kept.ChunksReceived.Count);
            Assert.Equal(2, refused.ChunksReceived.Count);
            Assert.All(kept.ChunksReceived.Concat(refused.ChunksReceived), chunk => Assert.All(chunk, b => Assert.Equal(0, b)));
        }

        [Fact]
        public async Task AWriteThatTheCardAnsweredButDidNotKeep_IsFoundByTheReadBack()
        {
            var reader = new UltralightPcscTestReader(UltralightVariant.Plain);
            reader.IgnoredWrites.Add(9);

            var result = await reader.WriteMifareUltralightPagesAsync(Plain(), 8, Convert.FromHexString("A1B2C3D401020304"));

            Assert.Equal(ERROR.Unknown, result.Error);
            Assert.Equal(9, result.FailedPage);
            Assert.Equal(2, result.PagesDone);
        }

        // ── the key of an Ultralight C ──

        [Fact]
        public async Task TheKeyOfAnUltralightC_IsSentOnce_As16Bytes_AndTheBytesAreCleared()
        {
            var reader = new UltralightPcscTestReader(UltralightVariant.C) { AcceptedKey = CKey };
            var access = new UltralightAccess(UltralightExpectation.C, UltralightAuth.UltralightC, CKeyHex);

            var result = await reader.ReadMifareUltralightPagesAsync(access, 4, 2);

            Assert.True(result.Succeeded);
            Assert.True(result.Authenticated);
            Assert.Equal(new[] { "select", "authC", "read 4", "read 5" }, reader.Calls);
            Assert.Equal(16, Assert.Single(reader.SecretsReceived).Length);
            Assert.All(reader.SecretsReceived, secret => Assert.All(secret, b => Assert.Equal(0, b)));
        }

        [Fact]
        public async Task AWrongKey_IsTriedOnce_IsAnAuthFailure_AndNothingIsReadAfterIt()
        {
            var reader = new UltralightPcscTestReader(UltralightVariant.C) { AcceptedKey = new byte[16] };
            var access = new UltralightAccess(UltralightExpectation.C, UltralightAuth.UltralightC, CKeyHex);

            var result = await reader.ReadMifareUltralightPagesAsync(access, 4, 2);

            Assert.Equal(ERROR.AuthFailure, result.Error);
            Assert.False(result.Authenticated);
            Assert.Equal(new[] { "select", "authC" }, reader.Calls);
            Assert.All(reader.SecretsReceived, secret => Assert.All(secret, b => Assert.Equal(0, b)));
        }

        [Theory]
        [InlineData(CardException.ErrorType.SECURITY_STATUS)]
        [InlineData(CardException.ErrorType.COMMAND_NOT_ALLOWED)]
        public async Task ACardThatRefusesTheAuthenticationCommand_IsAnAuthFailure_WhateverCodeItAnswersWith(CardException.ErrorType code)
        {
            var reader = new UltralightPcscTestReader(UltralightVariant.C) { AcceptedKey = new byte[16], WrongKeyCode = code };
            var access = new UltralightAccess(UltralightExpectation.C, UltralightAuth.UltralightC, CKeyHex);

            Assert.Equal(ERROR.AuthFailure, (await reader.AuthenticateMifareUltralightAsync(access)).Error);
        }

        [Fact]
        public async Task AKeyForAChipThatIsPlain_IsRefusedAfterTheChipWasFound_AndNeverSent()
        {
            var reader = new UltralightPcscTestReader(UltralightVariant.Plain) { AcceptedKey = CKey };
            var access = new UltralightAccess(UltralightExpectation.Auto, UltralightAuth.UltralightC, CKeyHex);

            // Authenticating needs a named chip: the key of an Ultralight C is not what any other chip wants.
            Assert.Equal(ERROR.ProtocolConstraint, (await reader.AuthenticateMifareUltralightAsync(access)).Error);
            Assert.Empty(reader.Calls);

            var named = new UltralightAccess(UltralightExpectation.C, UltralightAuth.UltralightC, CKeyHex);

            Assert.Equal(ERROR.ProtocolConstraint, (await reader.AuthenticateMifareUltralightAsync(named)).Error);
            Assert.Equal(new[] { "select" }, reader.Calls);
            Assert.Empty(reader.SecretsReceived);
        }

        // ── the log, and the old call ──

        [Fact]
        public async Task NoKeyOrExceptionTextIsLogged_WhateverHappens()
        {
            using (var logs = CapturedLogs.Install())
            {
                var reader = new UltralightPcscTestReader(UltralightVariant.C) { AcceptedKey = new byte[16] };
                var access = new UltralightAccess(UltralightExpectation.C, UltralightAuth.UltralightC, CKeyHex);

                await reader.ReadMifareUltralightPagesAsync(access, 4, 1);

                reader.SelectThrows = new IOException("SCardTransmit failed A1B2C3D4");
                await reader.ReadMifareUltralightPagesAsync(access, 4, 1);

                var written = string.Join("\n", logs.All().Select(CapturedLogs.EverythingWrittenFor));

                Assert.NotEmpty(logs.All());
                Assert.DoesNotContain("00112233445566778899AABBCCDDEEFF", written, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("8899AABB", written, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("A1B2C3D4", written, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("SCardTransmit", written, StringComparison.OrdinalIgnoreCase);
            }
        }

        // ── the chip that ReadChipPublic describes ──

        [Fact]
        public void TheMaskOfTheClassicChips_AlsoMatchesTheUltralight_SoTheUltralightIsDescribedFirst()
        {
            // 0x9000 & 0x1000 == 0x1000: tested with the mask of the Classic chips first, an Ultralight on the reader was described as
            // a Classic chip. This pins the reason for the order in ReadChipPublic.
            Assert.Equal(CARD_TYPE.MifareClassic, CARD_TYPE.MifareUltralight & CARD_TYPE.MifareClassic);
            Assert.Equal(CARD_TYPE.MifareClassic, CARD_TYPE.MifareUltralightC & CARD_TYPE.MifareClassic);
        }

        [Theory]
        [InlineData("MifareUltralight", CARD_TYPE.MifareUltralight)]
        [InlineData("MifareUltralightC", CARD_TYPE.MifareUltralightC)]
        public void AnUltralightOnTheReader_IsDescribedAsAnUltralight_NotAsAClassicChip(string cardType, CARD_TYPE expected)
        {
            var chip = LibLogicalAccessProvider.DescribeUltralightOrUnknownChip("04112233445566", cardType);

            var ultralight = Assert.IsType<RFiDGear.Models.MifareUltralightChipModel>(chip);
            Assert.Equal(expected, ultralight.CardType);
            Assert.Equal("04112233445566", ultralight.UID);
        }

        [Theory]
        [InlineData("__UNSUPPORTED__UNKNOWN")]
        [InlineData("SomethingLibLogicalAccessAddsLater")]
        [InlineData("4096")]
        [InlineData("")]
        [InlineData(null)]
        public void AChipThatTheApplicationHasNoTypeFor_IsAnUnspecifiedChipWithItsUid_NotAnError(string cardType)
        {
            // An NTAG215 is "__UNSUPPORTED__UNKNOWN" to LibLogicalAccess; Enum.Parse threw for it, and the read ended TransportError.
            // A number is no name: 4096 is the value of MifareClassic and must not be taken for it.
            var chip = LibLogicalAccessProvider.DescribeUltralightOrUnknownChip("04112233445566", cardType);

            Assert.NotNull(chip);
            Assert.Equal(typeof(RFiDGear.Models.GenericChipModel), chip.GetType());
            Assert.Equal(CARD_TYPE.Unspecified, chip.CardType);
            Assert.Equal("04112233445566", chip.UID);
        }

        [Theory]
        [InlineData("Mifare1K")]
        [InlineData("DESFireEV1")]
        public void AChipThatNeedsTheCommandsOfTheCard_IsLeftToTheCode_ThatReadsThem(string cardType)
        {
            Assert.Null(LibLogicalAccessProvider.DescribeUltralightOrUnknownChip("04112233445566", cardType));
        }

        [Fact]
        public async Task ReadingOnePage_TheOldWay_LeavesThePageForTheCaller()
        {
            var reader = new UltralightPcscTestReader(UltralightVariant.Plain);

            var error = await reader.ReadMifareUltralightSinglePage(5);

            Assert.Equal(ERROR.NoError, error);
            Assert.Equal(reader.PagesOf(5, 1), reader.MifareUltralightPageData);
        }

        [Fact]
        public async Task ReadingOnePage_TheOldWay_WithoutAChip_IsNoLongerASuccess()
        {
            // The old code answered NoError when no chip was there and no page was read.
            var reader = new UltralightPcscTestReader(UltralightVariant.Plain) { ChipOnReader = false };

            Assert.Equal(ERROR.TransportError, await reader.ReadMifareUltralightSinglePage(5));
        }
    }
}
