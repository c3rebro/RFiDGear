#nullable enable
using RFiDGear.Contracts;
using RFiDGear.Infrastructure;
using RFiDGear.Infrastructure.Tasks;
using RFiDGear.ViewModel.TaskSetupViewModels;

using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Xunit.Sdk;

namespace RFiDGear.HIL.Tests
{
    /// <summary>What a person at the reader is asked to do, and how they say they did it.</summary>
    internal interface IReaderOperator
    {
        /// <summary>Shows the instruction and returns when the person has done it.</summary>
        /// <param name="instruction">What to do with the card, in a sentence or two.</param>
        /// <returns><c>true</c> when the person confirmed, <c>false</c> when they cancelled.</returns>
        Task<bool> ConfirmAsync(string instruction);
    }

    /// <summary>
    /// The hardware cases of the reader scan (Slice 4) as plain steps against the extension host. The hardware tests run
    /// them with a real reader and a person who moves the cards; <c>RFiDGear.Tests</c> compiles this file too and runs
    /// the same steps against a scripted reader, so that an assertion which is wrong is found there and not at the
    /// reader. For that it depends on the contract, the host and xunit only.
    /// </summary>
    internal static class ReaderScenarios
    {
        /// <summary>
        /// A scan always answers: it does not throw, the answer is <see cref="ReaderScanState.Ready"/> or
        /// <see cref="ReaderScanState.NoReader"/>, it becomes the current picture, and a card on the reader comes first.
        /// Needs a reader. With an Elatec reader it works with or without a card; a PC/SC reader is found only with a card
        /// on it, so one must be there when the tests start.
        /// </summary>
        /// <param name="host">The host whose reader is scanned.</param>
        /// <returns>The task of the case.</returns>
        public static async Task AScanAlwaysAnswersAsync(IRFiDGearExtensionHost host)
        {
            var scan = await ScanAsync(host);

            Require(
                scan.State == ReaderScanState.Ready || scan.State == ReaderScanState.NoReader,
                $"The scan answered {scan.State}. Is another program or another RFiDGear window using the reader?");
            Require(scan.Equals(host.Reader.CurrentSnapshot), "The answer of the scan did not become the current picture of the reader.");

            if (scan.HasCard)
            {
                Require(scan.Credentials[0].IsPrimary, "The first credential is not the primary one.");
                Require(scan.Credentials.Skip(1).All(credential => !credential.IsPrimary), "A credential other than the first is marked primary.");
            }
        }

        /// <summary>
        /// With a card on the reader: the scan reports its UID (upper-case hex) and its type, the answer is the current
        /// picture, and the first picture of a host raises <c>ReaderStateChanged</c> once.
        /// </summary>
        /// <param name="host">A new host, which has not scanned yet.</param>
        /// <returns>The task of the case.</returns>
        public static async Task ACardOnTheReaderIsReportedAsync(IRFiDGearExtensionHost host)
        {
            using var events = new EventCounter(host.Reader);

            var scan = await ScanAsync(host);

            RequireCard(scan);
            var main = scan.Credentials[0];
            Require(main.IsPrimary, "The credential is not marked primary.");
            Require(Regex.IsMatch(main.Uid, "^[0-9A-F]+$"), $"The UID '{main.Uid}' is not upper-case hexadecimal.");
            Require(main.TypeCode != 0, "The card type has no code.");
            Require(
                !string.IsNullOrWhiteSpace(main.DisplayName) && main.DisplayName != "Unknown",
                $"The card type has no name (code {main.TypeCode}).");
            Require(scan.Equals(host.Reader.CurrentSnapshot), "The answer of the scan did not become the current picture of the reader.");
            Require(events.Count == 1, $"The first picture of a host raises ReaderStateChanged once, but it was raised {events.Count} times.");
        }

        /// <summary>A second scan of a card that did not move gives the same picture and raises nothing.</summary>
        /// <param name="host">A new host.</param>
        /// <returns>The task of the case.</returns>
        public static async Task ASecondScanOfTheSameCardRaisesNothingAsync(IRFiDGearExtensionHost host)
        {
            var first = await ScanAsync(host);
            RequireCard(first);
            using var events = new EventCounter(host.Reader);

            var second = await ScanAsync(host);

            Require(first.Equals(second), "The second scan found another picture. Did the card move?");
            Require(events.Count == 0, $"ReaderStateChanged was raised {events.Count} times for a picture that did not change.");
        }

        /// <summary>
        /// A scan while the host runs tasks is refused as busy, says nothing about the card, changes nothing and raises
        /// nothing, and scans again once the run is over. Works with or without a card on an Elatec reader (a PC/SC reader
        /// is found only with a card on it).
        /// </summary>
        /// <param name="host">A new host.</param>
        /// <returns>The task of the case.</returns>
        public static async Task AScanDuringARunIsAnsweredBusyAsync(IRFiDGearExtensionHost host)
        {
            var before = await ScanAsync(host);
            Require(before.State == ReaderScanState.Ready, $"The scan before the run answered {before.State}.");
            var cardType = before.HasCard ? (CARD_TYPE)before.Credentials[0].TypeCode : CARD_TYPE.Unspecified;
            using var events = new EventCounter(host.Reader);

            // The run takes the admission of the host before ExecuteAllAsync returns its task, and keeps it for as long as
            // the run lasts (it reads the card first), so the scan that follows at once is refused.
            host.Project.ReplaceTaskCollection(new object[]
            {
                new GenericChipTaskViewModel
                {
                    CurrentTaskIndex = "0",
                    SelectedTaskType = TaskType_GenericChipTask.ChipIsOfType,
                    SelectedChipType = cardType
                }
            });
            var run = host.Execution.ExecuteAllAsync(ExecutionRequest.Empty);

            ReaderSnapshot during;
            try
            {
                during = await ScanAsync(host);
            }
            finally
            {
                await run;
                host.Execution.ResetStatus();
            }

            Require(during.State == ReaderScanState.Busy, $"A scan during a run answered {during.State}, not Busy.");
            Require(!during.HasCard, "A busy answer lists a card. It must say nothing about the card.");
            Require(before.Equals(host.Reader.CurrentSnapshot), "The busy answer changed the current picture.");
            Require(events.Count == 0, $"ReaderStateChanged was raised {events.Count} times for a refused scan.");

            var after = await ScanAsync(host);
            Require(after.State == ReaderScanState.Ready, $"The scan after the run answered {after.State}, not Ready.");
        }

        /// <summary>
        /// A card that is taken off the reader is reported as gone, once. Asks the person to put a card on the reader and
        /// to take it off again.
        /// </summary>
        /// <param name="host">A new host.</param>
        /// <param name="person">Who moves the card.</param>
        /// <returns>The task of the case.</returns>
        public static async Task ARemovedCardIsReportedAsGoneAsync(IRFiDGearExtensionHost host, IReaderOperator person)
        {
            await AskAsync(person, "Put a card that RFiDGear can read on the reader, then click OK.");
            var present = await ScanAsync(host);
            RequireCard(present);
            using var events = new EventCounter(host.Reader);

            await AskAsync(person, "Take the card off the reader (leave the reader connected), then click OK.");
            var gone = await ScanAsync(host);

            Require(gone.State == ReaderScanState.Ready, $"The scan after the removal answered {gone.State}, not Ready.");
            Require(!gone.HasCard, $"The card is still reported ({string.Join(", ", gone.Credentials.Select(credential => credential.DisplayName))}) although it was taken off.");
            Require(events.Count == 1, $"ReaderStateChanged was raised {events.Count} times for the removal, not once.");
            Require(gone.Equals(host.Reader.CurrentSnapshot), "The answer of the scan did not become the current picture of the reader.");
        }

        /// <summary>
        /// A card that is replaced by another is reported with its own UID, once. Asks the person to put one card on the
        /// reader and then to replace it with a different one.
        /// </summary>
        /// <param name="host">A new host.</param>
        /// <param name="person">Who moves the cards.</param>
        /// <returns>The task of the case.</returns>
        public static async Task AReplacedCardIsReportedWithItsOwnUidAsync(IRFiDGearExtensionHost host, IReaderOperator person)
        {
            await AskAsync(person, "Put a card that RFiDGear can read on the reader, then click OK.");
            var first = await ScanAsync(host);
            RequireCard(first);
            using var events = new EventCounter(host.Reader);

            await AskAsync(person, "Replace it with a DIFFERENT card that RFiDGear can read (another UID), then click OK.");
            var second = await ScanAsync(host);

            RequireCard(second);
            Require(
                !string.Equals(first.Credentials[0].Uid, second.Credentials[0].Uid, StringComparison.Ordinal),
                "The second card has the same UID as the first. Use a different card.");
            Require(events.Count == 1, $"ReaderStateChanged was raised {events.Count} times for the replacement, not once.");
            Require(second.Equals(host.Reader.CurrentSnapshot), "The answer of the scan did not become the current picture of the reader.");
        }

        /// <summary>
        /// A card of a kind RFiDGear has no model for (an HF card that is not a MIFARE card) replaces a MIFARE card: the
        /// old card must not be reported any more. This is what the Elatec fix of the reader scan guards (the tag of the
        /// read before used to stay). The new card itself is not listed. Asks the person to move the cards.
        /// <para>
        /// The case proves the fix only if the reader detects the second card (it usually beeps, or its LED reacts): a card
        /// that the reader does not read gives the same picture with the fix and without it.
        /// </para>
        /// </summary>
        /// <param name="host">A new host.</param>
        /// <param name="person">Who moves the cards.</param>
        /// <returns>The task of the case.</returns>
        public static async Task ACardThatIsNotMifareDoesNotLeaveTheOldOneBehindAsync(IRFiDGearExtensionHost host, IReaderOperator person)
        {
            await AskAsync(person, "Put a MIFARE card (DESFire, Classic or Ultralight) on the reader, then click OK.");
            var mifare = await ScanAsync(host);
            RequireCard(mifare);
            var oldUid = mifare.Credentials[0].Uid;
            using var events = new EventCounter(host.Reader);

            await AskAsync(
                person,
                "Replace it with an HF card that is NOT a MIFARE card, but that this reader can read (for example ISO 15693; " +
                "iCLASS only if your reader reads it). The reader should beep or show its LED. Then click OK.");
            var other = await ScanAsync(host);

            Require(other.State == ReaderScanState.Ready, $"The scan after the replacement answered {other.State}, not Ready.");
            Require(
                other.Credentials.All(credential => !string.Equals(credential.Uid, oldUid, StringComparison.Ordinal)),
                "The MIFARE card is still reported although another card replaced it.");
            Require(events.Count == 1, $"ReaderStateChanged was raised {events.Count} times for the replacement, not once.");
        }

        // A read that has started cannot be limited: ScanAsync honours its token only until the read begins. A run that
        // hangs is ended with 'dotnet test --blame-hang-timeout 5m'.
        private static Task<ReaderSnapshot> ScanAsync(IRFiDGearExtensionHost host) => host.Reader.ScanAsync();

        private static async Task AskAsync(IReaderOperator person, string instruction)
        {
            if (!await person.ConfirmAsync(instruction))
            {
                throw new XunitException("The person at the reader cancelled the test.");
            }
        }

        private static void RequireCard(ReaderSnapshot scan)
        {
            Require(
                scan.State == ReaderScanState.Ready,
                $"The scan answered {scan.State}, not Ready. Is another program or another RFiDGear window using the reader?");
            Require(scan.HasCard, "No card was found. Put a card that RFiDGear can read on the reader and try again.");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
            {
                throw new XunitException(message);
            }
        }

        /// <summary>Counts the times <c>ReaderStateChanged</c> was raised. The event is raised before a scan returns.</summary>
        private sealed class EventCounter : IDisposable
        {
            private readonly IReaderHost _reader;
            private int _count;

            public EventCounter(IReaderHost reader)
            {
                _reader = reader;
                _reader.ReaderStateChanged += OnChanged;
            }

            public int Count => Volatile.Read(ref _count);

            public void Dispose() => _reader.ReaderStateChanged -= OnChanged;

            private void OnChanged(object? sender, EventArgs e) => Interlocked.Increment(ref _count);
        }
    }
}
