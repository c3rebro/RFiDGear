#nullable enable
using RFiDGear.Infrastructure;
using RFiDGear.Infrastructure.ReaderProviders;
using RFiDGear.Infrastructure.Ultralight;
using RFiDGear.Services.TaskExecution;
using RFiDGear.ViewModel.TaskSetupViewModels;

using System;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;

using Xunit;
using Xunit.Sdk;

namespace RFiDGear.HIL.Tests
{
    /// <summary>
    /// The steps of the Ultralight / NTAG hardware cases. They run the task as its dialog does (the command of its task type) on the
    /// reader that the application has, so the unit tests run the same steps against fakes of the reader, in memory
    /// (<c>HilUltralightScenarioSelfCheckTests</c>), and a step that is wrong is found before it reaches a card.
    /// <para>
    /// What a case may expect depends on the reader and the chip: an Elatec reader reads and writes every chip of the family whose
    /// layout is known, a PC/SC reader only the plain Ultralight and the Ultralight C (LibLogicalAccess has no name for the
    /// others), and a chip of another family is not a case at all. A case that a chip refuses asserts the refusal, so that "the
    /// reader does not support this chip" is checked as hard as "it works".
    /// </para>
    /// <para>
    /// No case sends a key or a password to a card, and the cases that write put back what they changed.
    /// </para>
    /// </summary>
    internal static class UltralightScenarios
    {
        /// <summary>What a reader gives of a chip.</summary>
        internal enum Support
        {
            /// <summary>The reader reads and writes the chip.</summary>
            Full,

            /// <summary>The reader refuses the chip: a protocol constraint, nothing is sent to the card.</summary>
            Refused,

            /// <summary>The chip is not of the Ultralight family.</summary>
            NotOfTheFamily,
        }

        /// <summary>What a task answers and leaves after it ran, as the dialog shows it.</summary>
        /// <param name="Error">The error of the task.</param>
        /// <param name="PagesDone">The pages the reader read or wrote.</param>
        /// <param name="Data">The bytes of a read; <see langword="null"/> before one.</param>
        /// <param name="Variant">The chip the reader found, when it found one.</param>
        /// <param name="Status">The status line: numbers and the name of the error, never data.</param>
        internal sealed record Answer(ERROR Error, int PagesDone, byte[]? Data, UltralightVariant? Variant, string Status)
        {
            /// <summary>Says what the task answered, for the message of a case that failed.</summary>
            /// <returns>The error, the number of pages and the status line.</returns>
            public string Describe() => $"error {Error}, pages done {PagesDone}, variant {Variant?.ToString() ?? "none"}, status: {Status.Trim()}";
        }

        /// <summary>
        /// Says what a reader gives of a chip, from the rules of the reader layers: a reader of any kind refuses a chip of the family
        /// whose layout is not confirmed; a PC/SC reader gives only the plain Ultralight and the Ultralight C.
        /// </summary>
        /// <param name="pcsc">Whether the reader is a PC/SC reader.</param>
        /// <param name="type">The chip type the reader reported.</param>
        /// <returns>What the reader gives.</returns>
        internal static Support SupportOf(bool pcsc, CARD_TYPE type)
        {
            if (!UltralightChipTypes.IsOfTheFamily(type))
            {
                return Support.NotOfTheFamily;
            }

            if (!UltralightChipTypes.TryGetVariant(type, out var variant))
            {
                return Support.Refused;
            }

            return !pcsc || variant == UltralightVariant.Plain || variant == UltralightVariant.C ? Support.Full : Support.Refused;
        }

        /// <summary>
        /// Reads the first user pages with the chip left open and with it named: the answer is the pages, or the refusal that the
        /// reader gives for a chip it does not support.
        /// </summary>
        /// <param name="type">The chip type on the reader.</param>
        /// <returns>A task that ends with the case.</returns>
        internal static async Task AReadOfTheUserPagesAsync(CARD_TYPE type)
        {
            var support = RequireTheFamily(type);

            foreach (var expected in new[] { UltralightExpectation.Auto, UltralightChipTypes.ExpectationOf(type) })
            {
                var answer = await RunAsync(HilUltralightTasks.Read(expected, UltralightMemoryMap.FirstUserPage, 4));
                var what = $"Reading 4 pages from page {UltralightMemoryMap.FirstUserPage}, expecting {expected}, on a {type} (support: {support})";

                if (support == Support.Full)
                {
                    Assert.True(answer.Error == ERROR.NoError, $"{what}: {answer.Describe()}");
                    Assert.True(answer.PagesDone == 4, $"{what}: {answer.Describe()}");
                    Assert.True(answer.Data?.Length == 4 * UltralightMemoryMap.PageSize, $"{what} gave no 16 bytes: {answer.Describe()}");
                    Assert.True(answer.Variant.HasValue, $"{what} did not say which chip it found: {answer.Describe()}");
                }
                else
                {
                    Assert.True(answer.Error == ERROR.ProtocolConstraint, $"{what} should be refused: {answer.Describe()}");
                    Assert.True(answer.PagesDone == 0, $"{what} should read nothing: {answer.Describe()}");
                }
            }
        }

        /// <summary>
        /// A task that expects another chip than the one on the reader ends as a protocol constraint, and reads nothing.
        /// </summary>
        /// <param name="type">The chip type on the reader.</param>
        /// <returns>A task that ends with the case.</returns>
        internal static async Task AnExpectationThatIsNotTheChipIsRefusedAsync(CARD_TYPE type)
        {
            if (RequireTheFamily(type) != Support.Full)
            {
                throw SkipException.ForSkip($"The reader gives no pages of a {type}, so there is no chip to name wrongly.");
            }

            var found = await RunAsync(HilUltralightTasks.Read(UltralightExpectation.Auto, UltralightMemoryMap.FirstUserPage, 1));
            Assert.True(found.Error == ERROR.NoError && found.Variant.HasValue, $"Finding out which chip it is: {found.Describe()}");

            // Another chip with a layout: the plain Ultralight, or an NTAG215 when it is one.
            var other = found.Variant == UltralightVariant.Plain ? UltralightExpectation.Ntag215 : UltralightExpectation.Plain;
            var wrong = await RunAsync(HilUltralightTasks.Read(other, UltralightMemoryMap.FirstUserPage, 1));

            Assert.True(wrong.Error == ERROR.ProtocolConstraint, $"Expecting {other} on a chip that is {found.Variant} should be refused: {wrong.Describe()}");
            Assert.True(wrong.PagesDone == 0 && (wrong.Data == null || wrong.Data.Length == 0), $"A refused read should give nothing: {wrong.Describe()}");
        }

        /// <summary>
        /// Writes one page of the user memory with other bytes, reads it back, and puts the old bytes back. A reader that refuses the
        /// chip is shown to write nothing.
        /// </summary>
        /// <param name="type">The chip type on the reader.</param>
        /// <returns>A task that ends with the case.</returns>
        internal static async Task AWriteToAUserPageIsReadBackAndPutBackAsync(CARD_TYPE type)
        {
            var support = RequireTheFamily(type);
            var expected = UltralightChipTypes.ExpectationOf(type);
            var page = HilUltralightTasks.UserPage;

            if (support != Support.Full)
            {
                var refused = await RunAsync(HilUltralightTasks.Write(expected, page, new byte[UltralightMemoryMap.PageSize]));

                Assert.True(refused.Error == ERROR.ProtocolConstraint, $"Writing page {page} of a {type} should be refused: {refused.Describe()}");
                Assert.True(refused.PagesDone == 0, $"A refused write should write nothing: {refused.Describe()}");

                return;
            }

            var before = await ReadPageAsync(expected, page, "Reading the page before");

            // Every byte differs from what it was, so a write that did not happen cannot look like one that did.
            var pattern = before.Select(value => (byte)(value ^ 0xA5)).ToArray();
            Exception? failure = null;
            var mayHaveWritten = false;

            try
            {
                mayHaveWritten = true;
                var write = await RunAsync(HilUltralightTasks.Write(expected, page, pattern));
                Assert.True(write.Error == ERROR.NoError && write.PagesDone == 1, $"Writing page {page}: {write.Describe()}");

                var back = await ReadPageAsync(expected, page, "Reading the page back");
                Assert.True(back.SequenceEqual(pattern), $"Page {page} does not hold what was written");
            }
            catch (Exception ex)
            {
                failure = ex;
            }

            // Put back, also after a write that failed half way.
            if (mayHaveWritten)
            {
                var restore = await RunAsync(HilUltralightTasks.Write(expected, page, before));
                var restored = restore.Error == ERROR.NoError ? await TryReadPageAsync(expected, page) : null;

                if (restored == null || !restored.SequenceEqual(before))
                {
                    throw new InvalidOperationException($"Page {page} could NOT be put back to what it held: {restore.Describe()}. It has to be set by hand.", failure);
                }
            }

            if (failure != null)
            {
                ExceptionDispatchInfo.Capture(failure).Throw();
            }
        }

        /// <summary>
        /// A write to a protected page is refused before the card is asked, with the chip named and without the opt-in, and with the
        /// opt-in and no chip named. The bytes are zeros, so that not even a broken refusal would change a page: the one-time
        /// programmable bytes are only ever set, never cleared, and a write of zeros sets nothing.
        /// </summary>
        /// <param name="type">The chip type on the reader.</param>
        /// <returns>A task that ends with the case.</returns>
        internal static async Task AWriteToAProtectedPageIsRefusedBeforeTheCardAsync(CARD_TYPE type)
        {
            var support = RequireTheFamily(type);
            var expected = UltralightChipTypes.ExpectationOf(type);

            if (expected == UltralightExpectation.Auto)
            {
                throw SkipException.ForSkip($"The layout of a {type} is not known, so there is no chip to name and no protected page to write to.");
            }

            var page = HilUltralightTasks.ProtectedPage;
            var zeros = new byte[UltralightMemoryMap.PageSize];
            var before = support == Support.Full ? await TryReadPageAsync(expected, page) : null;

            var withoutTheOptIn = await RunAsync(HilUltralightTasks.Write(expected, page, zeros));
            var withoutTheChip = await RunAsync(HilUltralightTasks.Write(UltralightExpectation.Auto, page, zeros, allowProtectedPages: true));

            Assert.True(withoutTheOptIn.Error == ERROR.ProtocolConstraint && withoutTheOptIn.PagesDone == 0, $"A write to page {page} without the opt-in should be refused: {withoutTheOptIn.Describe()}");
            Assert.True(withoutTheChip.Error == ERROR.ProtocolConstraint && withoutTheChip.PagesDone == 0, $"A write to page {page} with the opt-in and no chip named should be refused: {withoutTheChip.Describe()}");

            if (before != null)
            {
                var after = await TryReadPageAsync(expected, page);

                Assert.True(after != null && after.SequenceEqual(before), $"Page {page} changed although both writes were refused");
            }
        }

        // The chip must be of the family: another card on the reader is no case, and the test is reported as skipped.
        private static Support RequireTheFamily(CARD_TYPE type)
        {
            var reader = ReaderDevice.Instance ?? throw SkipException.ForSkip("No reader.");
            var support = SupportOf(reader.IsPcscReader, type);

            if (support == Support.NotOfTheFamily)
            {
                throw SkipException.ForSkip($"The card on the reader is a {type}, which is not a chip of the Ultralight family. Put an Ultralight, Ultralight C, Ultralight EV1 or NTAG card on the reader.");
            }

            return support;
        }

        private static async Task<byte[]> ReadPageAsync(UltralightExpectation expected, int page, string what)
        {
            var answer = await RunAsync(HilUltralightTasks.Read(expected, page, 1));

            Assert.True(answer.Error == ERROR.NoError && answer.Data?.Length == UltralightMemoryMap.PageSize, $"{what}: {answer.Describe()}");

            return answer.Data!;
        }

        private static async Task<byte[]?> TryReadPageAsync(UltralightExpectation expected, int page)
        {
            var answer = await RunAsync(HilUltralightTasks.Read(expected, page, 1));

            return answer.Error == ERROR.NoError && answer.Data?.Length == UltralightMemoryMap.PageSize ? answer.Data : null;
        }

        // Runs the task as its dialog does and says what it answered.
        private static async Task<Answer> RunAsync(MifareUltralightSetupViewModel task)
        {
            await task.CommandDelegator.ExecuteAsync(task.SelectedTaskType);

            var details = ((ITaskOutcomeDetails)task).GetOutcomeDetails();
            var pagesDone = details.TryGetValue("PagesDone", out var done) && done is int number ? number : 0;
            UltralightVariant? variant = details.TryGetValue("Variant", out var found) && found is int code ? (UltralightVariant)code : null;
            var data = task.LastReadData != null ? (byte[])task.LastReadData.Clone() : null;

            return new Answer(task.CurrentTaskErrorLevel, pagesDone, data, variant, task.StatusText ?? string.Empty);
        }
    }
}
