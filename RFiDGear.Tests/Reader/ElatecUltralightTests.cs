using System;
using System.Linq;
using System.Threading.Tasks;
using Elatec.NET;
using Elatec.NET.Cards.Mifare;
using RFiDGear.Infrastructure;
using RFiDGear.Infrastructure.ReaderProviders;
using RFiDGear.Infrastructure.Ultralight;
using Xunit;

namespace RFiDGear.Tests.Reader
{
    /// <summary>
    /// The page operations of the Elatec reader, against a reader in memory that answers like the one that was measured. What each
    /// test pins is a promise of the one-attempt rule or of the layouts: a call that cannot be made touches no card, a refusal ends
    /// the call and is never repeated, and the key arrays are cleared.
    /// </summary>
    public class ElatecUltralightTests
    {
        private static UltralightAccess Ntag215() => UltralightAccess.Open(UltralightExpectation.Ntag215);

        private static byte[] Pages(UltralightTestReader reader, int start, int count) => reader.PagesOf(start, count);

        // ── reading ──

        [Fact]
        public async Task ReadingTheLastPagesOfAnNtag215_ReturnsExactlyThosePages_WithoutWhatTheCardWrapsAroundTo()
        {
            var reader = new UltralightTestReader(UltralightVariant.Ntag215);

            var result = await reader.ReadMifareUltralightPagesAsync(Ntag215(), 130, 5);

            Assert.True(result.Succeeded);
            Assert.Equal(UltralightVariant.Ntag215, result.Variant);
            Assert.Equal(5, result.PagesDone);
            Assert.Equal(20, result.Data.Length);
            Assert.Equal(Pages(reader, 130, 5), result.Data);

            // One READ per four pages; the second one starts at page 134, and the 12 bytes it returns from page 0 on are dropped.
            Assert.Equal(new[] { "read 130", "read 134" }, reader.Calls.Where(c => c.StartsWith("read", StringComparison.Ordinal)));
        }

        [Fact]
        public async Task WithoutANamedChip_TheChipThatIsFound_IsTheOneThatIsUsed()
        {
            var reader = new UltralightTestReader(UltralightVariant.Ntag215);

            var result = await reader.ReadMifareUltralightPagesAsync(UltralightAccess.Open(), 4, 2);

            Assert.True(result.Succeeded);
            Assert.Equal(UltralightVariant.Ntag215, result.Variant);
            Assert.Equal(Pages(reader, 4, 2), result.Data);
            Assert.Equal(new[] { "select", "version", "read 4" }, reader.Calls);
        }

        [Fact]
        public async Task AChipThatRefusesGetVersion_IsAPlainUltralight_AfterItWasSelectedAgain()
        {
            var reader = new UltralightTestReader(UltralightVariant.Plain);

            var result = await reader.ReadMifareUltralightPagesAsync(UltralightAccess.Open(), 12, 4);

            Assert.True(result.Succeeded);
            Assert.Equal(UltralightVariant.Plain, result.Variant);
            Assert.Equal(Pages(reader, 12, 4), result.Data);

            // The refusal left the chip idle, so it was selected again before the first READ.
            Assert.Equal(new[] { "select", "version", "select", "read 12" }, reader.Calls);
        }

        [Fact]
        public async Task APlainUltralight_HasNoPage16_AndNothingIsReadToFindThatOut()
        {
            var reader = new UltralightTestReader(UltralightVariant.Plain);

            var result = await reader.ReadMifareUltralightPagesAsync(UltralightAccess.Open(), 15, 2);

            Assert.Equal(ERROR.ProtocolConstraint, result.Error);
            Assert.Equal(UltralightVariant.Plain, result.Variant);
            Assert.Equal(0, reader.CountStartingWith("read"));
        }

        [Fact]
        public async Task AnUltralightC_IsToldBySubType_AndGetVersionIsNotAskedOfIt()
        {
            var reader = new UltralightTestReader(UltralightVariant.C);

            var result = await reader.ReadMifareUltralightPagesAsync(UltralightAccess.Open(UltralightExpectation.C), 4, 4);

            Assert.True(result.Succeeded);
            Assert.Equal(UltralightVariant.C, result.Variant);
            Assert.Equal(0, reader.CountOf("version"));
        }

        [Fact]
        public async Task WhenTheLibraryCannotTellAnUltralightCFromAPlainOne_TheExpectationOfTheTaskDecides()
        {
            // Elatec.NET 0.7.0-beta.1 reports both as MifareUltralight, and neither answers GET_VERSION.
            var reader = new UltralightTestReader(UltralightVariant.C) { SubType = MifareChipSubType.MifareUltralight };

            var asC = await reader.ReadMifareUltralightPagesAsync(UltralightAccess.Open(UltralightExpectation.C), 4, 2);
            var asAuto = await reader.ReadMifareUltralightPagesAsync(UltralightAccess.Open(), 4, 2);
            var asPlain = await reader.ReadMifareUltralightPagesAsync(UltralightAccess.Open(UltralightExpectation.Plain), 4, 2);

            Assert.Equal(UltralightVariant.C, asC.Variant);
            Assert.Equal(UltralightVariant.Plain, asAuto.Variant);
            Assert.Equal(UltralightVariant.Plain, asPlain.Variant);
            Assert.True(asC.Succeeded && asAuto.Succeeded && asPlain.Succeeded);
        }

        [Fact]
        public async Task AnAnswerOfAChipThatIsNotInTheTable_RefusesTheCall_BeforeAnythingIsRead()
        {
            // NTAG210: the family is known, its layout is not.
            var reader = new UltralightTestReader(UltralightVariant.Ntag215) { VersionAnswer = Convert.FromHexString("0004040101000B03") };

            var result = await reader.ReadMifareUltralightPagesAsync(UltralightAccess.Open(), 4, 1);

            Assert.Equal(ERROR.ProtocolConstraint, result.Error);
            Assert.Null(result.Variant);
            Assert.Equal(0, reader.CountStartingWith("read"));
        }

        [Fact]
        public async Task ATagThatIsNoUltralightChip_IsRefused_AfterOneSelect()
        {
            var reader = new UltralightTestReader(UltralightVariant.Ntag215) { SubType = MifareChipSubType.DESFireEV1 };

            var result = await reader.ReadMifareUltralightPagesAsync(UltralightAccess.Open(), 4, 1);

            Assert.Equal(ERROR.ProtocolConstraint, result.Error);
            Assert.Equal(new[] { "select" }, reader.Calls);

            reader.SubType = null;
            Assert.Equal(ERROR.ProtocolConstraint, (await reader.ReadMifareUltralightPagesAsync(UltralightAccess.Open(), 4, 1)).Error);
        }

        [Fact]
        public async Task WithoutATag_TheTagIsAskedForThreeTimes_AndThenItIsATransportError()
        {
            var reader = new UltralightTestReader(UltralightVariant.Ntag215) { TagOnReader = false };

            var result = await reader.ReadMifareUltralightPagesAsync(Ntag215(), 4, 1);

            Assert.Equal(ERROR.TransportError, result.Error);
            Assert.Equal(3, reader.CountOf("select"));
            Assert.Equal(3, reader.Calls.Count);
        }

        [Fact]
        public async Task AReaderThatIsNotConnected_IsATransportError_AndNothingIsAsked()
        {
            var reader = new UltralightTestReader(UltralightVariant.Ntag215) { Connected = false };

            var result = await reader.ReadMifareUltralightPagesAsync(Ntag215(), 4, 1);

            Assert.Equal(ERROR.TransportError, result.Error);
            Assert.Empty(reader.Calls);
        }

        [Fact]
        public async Task AnotherChipThanTheTaskExpects_IsRefused_AndNothingIsRead()
        {
            var reader = new UltralightTestReader(UltralightVariant.Ntag215);

            var result = await reader.ReadMifareUltralightPagesAsync(UltralightAccess.Open(UltralightExpectation.Ntag213), 4, 1);

            Assert.Equal(ERROR.ProtocolConstraint, result.Error);
            Assert.Equal(UltralightVariant.Ntag215, result.Variant);
            Assert.Equal(0, reader.CountStartingWith("read"));
        }

        [Fact]
        public async Task APageThatTheNamedChipDoesNotHave_IsRefused_WithoutAnyCallToTheCard()
        {
            var reader = new UltralightTestReader(UltralightVariant.Ntag215);

            var result = await reader.ReadMifareUltralightPagesAsync(Ntag215(), 135, 1);

            Assert.Equal(ERROR.ProtocolConstraint, result.Error);
            Assert.Empty(reader.Calls);

            // Without a named chip it takes the card to know: the chip is asked for what it is, and nothing is read.
            var auto = await reader.ReadMifareUltralightPagesAsync(UltralightAccess.Open(), 135, 1);

            Assert.Equal(ERROR.ProtocolConstraint, auto.Error);
            Assert.Equal(0, reader.CountStartingWith("read"));
        }

        [Fact]
        public async Task ARefusedRead_EndsTheCall_WithThePagesBeforeIt_AndIsNeverRepeated()
        {
            var reader = new UltralightTestReader(UltralightVariant.Ntag215);
            reader.RefusedReads.Add(134);

            var result = await reader.ReadMifareUltralightPagesAsync(Ntag215(), 130, 5);

            // The tag is still there after the refusal: the card said no.
            Assert.Equal(ERROR.PermissionDenied, result.Error);
            Assert.Equal(4, result.PagesDone);
            Assert.Equal(134, result.FailedPage);
            Assert.Equal(Pages(reader, 130, 4), result.Data);
            Assert.Equal(2, reader.CountStartingWith("read"));
            Assert.Equal(1, reader.CountOf("present"));
        }

        [Fact]
        public async Task ARefusedRead_OfAChipThatWasTakenAway_IsATransportError()
        {
            var reader = new UltralightTestReader(UltralightVariant.Ntag215) { RemoveTagWhenAReadIsRefused = true };
            reader.RefusedReads.Add(130);

            var result = await reader.ReadMifareUltralightPagesAsync(Ntag215(), 130, 1);

            Assert.Equal(ERROR.TransportError, result.Error);
            Assert.Equal(130, result.FailedPage);
            Assert.Equal(0, result.PagesDone);
            Assert.Empty(result.Data);
        }

        [Fact]
        public async Task ReadingOnePage_TheOldWay_StillWorks_AndLeavesThePageForTheCaller()
        {
            var reader = new UltralightTestReader(UltralightVariant.Ntag215);

            var error = await reader.ReadMifareUltralightSinglePage(5);

            Assert.Equal(ERROR.NoError, error);
            Assert.Equal(Pages(reader, 5, 1), reader.MifareUltralightPageData);
        }

        // ── writing ──

        [Fact]
        public async Task AWriteOfUserPages_IsWritten_AndReadBackToCheckIt()
        {
            var reader = new UltralightTestReader(UltralightVariant.Ntag215);
            var data = Convert.FromHexString("A1B2C3D401020304");

            var result = await reader.WriteMifareUltralightPagesAsync(Ntag215(), 100, data);

            Assert.True(result.Succeeded);
            Assert.Equal(2, result.PagesDone);
            Assert.Equal(data, Pages(reader, 100, 2));
            Assert.Equal(new[] { "write 100", "write 101" }, reader.Calls.Where(c => c.StartsWith("write", StringComparison.Ordinal)));
            Assert.Equal(1, reader.CountStartingWith("read"));   // the check: one READ of four pages covers both
        }

        [Fact]
        public async Task AProtectedPage_IsNotWritten_WithoutTheOptIn_AndNothingIsAskedOfTheCard()
        {
            var reader = new UltralightTestReader(UltralightVariant.Ntag215);
            var before = (byte[])reader.Memory.Clone();

            var result = await reader.WriteMifareUltralightPagesAsync(Ntag215(), 130, new byte[4]);

            Assert.Equal(ERROR.ProtocolConstraint, result.Error);
            Assert.Empty(reader.Calls);
            Assert.Equal(before, reader.Memory);
        }

        [Fact]
        public async Task AProtectedPage_IsWritten_WithTheOptInAndANamedChip()
        {
            var reader = new UltralightTestReader(UltralightVariant.Ntag215);
            var access = new UltralightAccess(UltralightExpectation.Ntag215, allowProtectedPages: true);

            var result = await reader.WriteMifareUltralightPagesAsync(access, 130, new byte[] { 1, 2, 3, 4 });

            Assert.True(result.Succeeded);
            Assert.Equal(new byte[] { 1, 2, 3, 4 }, Pages(reader, 130, 1));
        }

        [Fact]
        public async Task TheOptIn_WithoutANamedChip_WritesNothing()
        {
            var reader = new UltralightTestReader(UltralightVariant.Ntag215);
            var access = new UltralightAccess(UltralightExpectation.Auto, allowProtectedPages: true);

            var result = await reader.WriteMifareUltralightPagesAsync(access, 100, new byte[4]);

            Assert.Equal(ERROR.ProtocolConstraint, result.Error);
            Assert.Empty(reader.Calls);
        }

        [Fact]
        public async Task AProtectedPageThatOnlyTheChipFoundShows_IsNotWritten_WithoutTheOptIn()
        {
            // Auto: the page is a user page of an NTAG213 and a config page of an MF0UL11. Here the chip is an MF0UL11.
            var reader = new UltralightTestReader(UltralightVariant.Ev1Mf0ul11);
            var before = (byte[])reader.Memory.Clone();

            var result = await reader.WriteMifareUltralightPagesAsync(UltralightAccess.Open(), 16, new byte[4]);

            Assert.Equal(ERROR.ProtocolConstraint, result.Error);
            Assert.Equal(UltralightVariant.Ev1Mf0ul11, result.Variant);
            Assert.Equal(0, reader.CountStartingWith("write"));
            Assert.Equal(before, reader.Memory);
        }

        [Fact]
        public async Task ARefusedWrite_EndsTheCall_WithTheNumberOfPagesWritten_AndNothingIsWrittenAgain()
        {
            var reader = new UltralightTestReader(UltralightVariant.Ntag215);
            reader.RefusedWrites.Add(101);

            var result = await reader.WriteMifareUltralightPagesAsync(Ntag215(), 100, new byte[12]);

            Assert.Equal(ERROR.PermissionDenied, result.Error);
            Assert.Equal(1, result.PagesDone);
            Assert.Equal(101, result.FailedPage);
            Assert.Equal(new[] { "write 100", "write 101" }, reader.Calls.Where(c => c.StartsWith("write", StringComparison.Ordinal)));
            Assert.Equal(0, reader.CountStartingWith("read"));
        }

        [Fact]
        public async Task AWriteThatTheCardAnsweredButDidNotKeep_IsFoundByTheReadBack()
        {
            var reader = new UltralightTestReader(UltralightVariant.Ntag215);
            reader.IgnoredWrites.Add(101);

            var result = await reader.WriteMifareUltralightPagesAsync(Ntag215(), 100, Convert.FromHexString("A1B2C3D401020304"));

            Assert.Equal(ERROR.Unknown, result.Error);
            Assert.Equal(101, result.FailedPage);
            Assert.Equal(2, result.PagesDone);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(3)]
        [InlineData(5)]
        public async Task DataThatIsNoWholeNumberOfPages_IsRefused_WithoutAnyCall(int length)
        {
            var reader = new UltralightTestReader(UltralightVariant.Ntag215);

            var result = await reader.WriteMifareUltralightPagesAsync(Ntag215(), 100, new byte[length]);

            Assert.Equal(ERROR.ProtocolConstraint, result.Error);
            Assert.Empty(reader.Calls);
            Assert.Equal(ERROR.ProtocolConstraint, (await reader.WriteMifareUltralightPagesAsync(Ntag215(), 100, null)).Error);
        }

        // ── authentication ──

        private static readonly byte[] Password = Convert.FromHexString("A1B2C3D4");

        [Fact]
        public async Task APassword_IsSentOnce_ThenTheReadGoesOn_AndTheBytesAreCleared()
        {
            var reader = new UltralightTestReader(UltralightVariant.Ntag215) { AcceptedKey = Password };
            var access = new UltralightAccess(UltralightExpectation.Ntag215, UltralightAuth.Password, "A1B2C3D4", "5566");

            var result = await reader.ReadMifareUltralightPagesAsync(access, 4, 2);

            Assert.True(result.Succeeded);
            Assert.True(result.Authenticated);
            Assert.Equal(new[] { "select", "version", "pwd", "read 4" }, reader.Calls);
            Assert.Equal(2, reader.SecretsReceived.Count);
            Assert.All(reader.SecretsReceived, secret => Assert.All(secret, b => Assert.Equal(0, b)));
        }

        [Fact]
        public async Task AWrongPassword_IsTriedOnce_IsAnAuthFailure_AndNothingIsReadAfterIt()
        {
            var reader = new UltralightTestReader(UltralightVariant.Ntag215) { AcceptedKey = Convert.FromHexString("00000000") };
            var access = new UltralightAccess(UltralightExpectation.Ntag215, UltralightAuth.Password, "A1B2C3D4", "5566");

            var result = await reader.ReadMifareUltralightPagesAsync(access, 4, 2);

            Assert.Equal(ERROR.AuthFailure, result.Error);
            Assert.False(result.Authenticated);
            Assert.Equal(1, reader.CountOf("pwd"));
            Assert.Equal(0, reader.CountStartingWith("read"));
            Assert.All(reader.SecretsReceived, secret => Assert.All(secret, b => Assert.Equal(0, b)));
        }

        [Fact]
        public async Task TheKeyOfAnUltralightC_IsSentOnce_As16Bytes()
        {
            var key = Convert.FromHexString("00112233445566778899AABBCCDDEEFF");
            var reader = new UltralightTestReader(UltralightVariant.C) { AcceptedKey = key };
            var access = new UltralightAccess(UltralightExpectation.C, UltralightAuth.UltralightC, "00112233445566778899AABBCCDDEEFF");

            var result = await reader.AuthenticateMifareUltralightAsync(access);

            Assert.True(result.Succeeded);
            Assert.True(result.Authenticated);
            Assert.Equal(new[] { "select", "authC" }, reader.Calls);
            Assert.Equal(16, reader.SecretsReceived.Single().Length);
        }

        [Theory]
        [InlineData("Auto", "Password", "A1B2C3D4", "5566")]        // authentication needs a named chip
        [InlineData("C", "Password", "A1B2C3D4", "5566")]           // a password is not what an Ultralight C takes
        [InlineData("Ntag215", "UltralightC", "00112233445566778899AABBCCDDEEFF", null)]
        [InlineData("Ntag215", "Password", "A1B2C3", "5566")]       // 3 bytes
        [InlineData("Ntag215", "Password", "A1B2C3D4", null)]       // no PACK
        [InlineData("Ntag215", "Password", "A1B2C3D4", "55")]
        [InlineData("Ntag215", "Password", "ZZB2C3D4", "5566")]     // not hexadecimal
        [InlineData("C", "UltralightC", "0011", null)]              // a key of the wrong length
        [InlineData("Plain", "Password", "A1B2C3D4", "5566")]       // a chip that has no authentication
        public async Task AnAuthenticationThatCannotBeRight_IsRefused_WithoutAnyCallToTheCard(string expected, string auth, string keyHex, string packHex)
        {
            var reader = new UltralightTestReader(UltralightVariant.Ntag215);
            var access = new UltralightAccess(Enum.Parse<UltralightExpectation>(expected), Enum.Parse<UltralightAuth>(auth), keyHex, packHex);

            var result = await reader.ReadMifareUltralightPagesAsync(access, 4, 1);

            Assert.Equal(ERROR.ProtocolConstraint, result.Error);
            Assert.Empty(reader.Calls);
        }

        [Fact]
        public async Task APasswordForAChipThatIsPlain_IsRefusedAfterTheChipWasFound_AndNeverSent()
        {
            // The task says NTAG215; the chip on the reader refuses GET_VERSION, which is an Ultralight.
            var reader = new UltralightTestReader(UltralightVariant.Plain);
            var access = new UltralightAccess(UltralightExpectation.Ntag215, UltralightAuth.Password, "A1B2C3D4", "5566");

            var result = await reader.ReadMifareUltralightPagesAsync(access, 4, 1);

            Assert.Equal(ERROR.ProtocolConstraint, result.Error);
            Assert.Equal(0, reader.CountOf("pwd"));
        }

        [Fact]
        public async Task Authenticating_WithoutSayingHow_IsRefused()
        {
            var reader = new UltralightTestReader(UltralightVariant.Ntag215);

            var result = await reader.AuthenticateMifareUltralightAsync(Ntag215());

            Assert.Equal(ERROR.ProtocolConstraint, result.Error);
            Assert.Empty(reader.Calls);
        }

        // ── faults, locks, logs ──

        [Fact]
        public async Task AFaultInTheReader_DuringTheSelection_IsAnErrorOfTheCall_AndNotRepeated()
        {
            var reader = new UltralightTestReader(UltralightVariant.Ntag215)
            {
                SelectThrows = new ReaderException("Call was not successfull, error NotOpen", null)
            };

            var result = await reader.ReadMifareUltralightPagesAsync(Ntag215(), 4, 1);

            Assert.Equal(ERROR.TransportError, result.Error);
            Assert.Equal(1, reader.CountOf("select"));
        }

        [Fact]
        public async Task AnUnexpectedFault_IsUnknown_AndTheReaderCanBeUsedAgainAfterIt()
        {
            var reader = new UltralightTestReader(UltralightVariant.Ntag215) { SelectThrows = new InvalidOperationException("boom") };

            Assert.Equal(ERROR.Unknown, (await reader.ReadMifareUltralightPagesAsync(Ntag215(), 4, 1)).Error);

            reader.SelectThrows = null;
            var again = await Task.WhenAny(reader.ReadMifareUltralightPagesAsync(Ntag215(), 4, 1), Task.Delay(TimeSpan.FromSeconds(10)));

            Assert.IsType<Task<UltralightResult>>(again);
            Assert.True(((Task<UltralightResult>)again).Result.Succeeded);
        }

        [Fact]
        public async Task ACancellation_IsNotSwallowed_AndTheReaderCanBeUsedAgainAfterIt()
        {
            var reader = new UltralightTestReader(UltralightVariant.Ntag215) { SelectThrows = new OperationCanceledException() };

            await Assert.ThrowsAsync<OperationCanceledException>(() => reader.ReadMifareUltralightPagesAsync(Ntag215(), 4, 1));

            reader.SelectThrows = null;
            var again = await Task.WhenAny(reader.ReadMifareUltralightPagesAsync(Ntag215(), 4, 1), Task.Delay(TimeSpan.FromSeconds(10)));

            Assert.True(((Task<UltralightResult>)again).Result.Succeeded);
        }

        [Fact]
        public async Task NoKeyPasswordOrPackIsLogged_WhateverHappens()
        {
            using (var logs = CapturedLogs.Install())
            {
                var reader = new UltralightTestReader(UltralightVariant.Ntag215) { AcceptedKey = Convert.FromHexString("00000000") };
                var access = new UltralightAccess(UltralightExpectation.Ntag215, UltralightAuth.Password, "A1B2C3D4", "5566");

                await reader.ReadMifareUltralightPagesAsync(access, 4, 1);

                reader.SelectThrows = new ReaderException("Call was not successfull, error NotOpen A1B2C3D4", null);
                await reader.ReadMifareUltralightPagesAsync(access, 4, 1);

                var written = string.Join("\n", logs.All().Select(CapturedLogs.EverythingWrittenFor));

                Assert.NotEmpty(logs.All());
                Assert.DoesNotContain("A1B2C3D4", written, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("5566", written, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("A1B2C3", written, StringComparison.OrdinalIgnoreCase);
            }
        }

        [Fact]
        public async Task AReaderThatHasNoPageOperations_AnswersProtocolConstraint()
        {
            // The PC/SC reader keeps the default of the base class until it gets its own operations.
            var reader = new LibLogicalAccessProvider();

            Assert.Equal(ERROR.ProtocolConstraint, (await reader.ReadMifareUltralightPagesAsync(UltralightAccess.Open(), 4, 1)).Error);
            Assert.Equal(ERROR.ProtocolConstraint, (await reader.WriteMifareUltralightPagesAsync(UltralightAccess.Open(), 4, new byte[4])).Error);
            Assert.Equal(ERROR.ProtocolConstraint, (await reader.AuthenticateMifareUltralightAsync(UltralightAccess.Open())).Error);
        }

        [Fact]
        public void TheAccess_NeverShowsItsSecrets()
        {
            var text = new UltralightAccess(UltralightExpectation.Ntag215, UltralightAuth.Password, "A1B2C3D4", "5566", allowProtectedPages: true).ToString();

            Assert.DoesNotContain("A1B2C3D4", text);
            Assert.DoesNotContain("5566", text);
            Assert.Contains("Ntag215", text);
        }
    }
}
