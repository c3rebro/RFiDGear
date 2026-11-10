#nullable enable
using RFiDGear.Contracts;
using RFiDGear.Infrastructure;
using RFiDGear.Infrastructure.Tasks;
using RFiDGear.ViewModel.TaskSetupViewModels;

using System;
using System.Collections.Generic;
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
        /// <summary>
        /// Shows the instruction and returns when the person has done it. For a step the reader cannot see (a card of a kind
        /// it does not list).
        /// </summary>
        /// <param name="instruction">What to do with the card, in a sentence or two.</param>
        /// <returns><c>true</c> when the person confirmed, <c>false</c> when they cancelled.</returns>
        Task<bool> ConfirmAsync(string instruction);

        /// <summary>
        /// Shows the instruction and keeps asking <paramref name="isDone"/> until it says yes, the time is up or the person
        /// cancels: nobody has to click when the step is done, the case watches the reader. For a step the reader can see.
        /// </summary>
        /// <param name="instruction">What to do with the card, in a sentence or two.</param>
        /// <param name="isDone">Looks at the reader once and says whether the card is where the instruction wanted it.</param>
        /// <param name="timeout">How long to wait for the person.</param>
        /// <returns>How the waiting ended.</returns>
        Task<WaitOutcome> WaitAsync(string instruction, Func<Task<bool>> isDone, TimeSpan timeout);
    }

    /// <summary>How the waiting for a step of the person ended.</summary>
    internal enum WaitOutcome
    {
        /// <summary>The reader showed what the person was asked for.</summary>
        Done,

        /// <summary>The time was up.</summary>
        TimedOut,

        /// <summary>The person cancelled.</summary>
        Cancelled
    }

    /// <summary>
    /// The hardware cases of the reader scan (Slice 4) as plain steps against the extension host. The hardware tests run
    /// them with a real reader and a person who moves the cards; <c>RFiDGear.Tests</c> compiles this file too and runs
    /// the same steps against a scripted reader, so that an assertion which is wrong is found there and not at the
    /// reader. For that it depends on the contract, the host and xunit only.
    /// </summary>
    internal static class ReaderScenarios
    {
        // The time a person gets for one step; each step ends by itself as soon as the reader shows what was asked for.
        private static readonly TimeSpan WaitLimit = TimeSpan.FromSeconds(60);

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
        /// A card that is taken off the reader is noticed, and every change of the picture on the way is announced exactly
        /// once. The case watches the reader: it scans until it sees a card, asks the person to take it off, and scans until
        /// it sees the reader empty, so nobody has to click when the physical step is done.
        /// </summary>
        /// <param name="host">A new host.</param>
        /// <param name="person">Who moves the card.</param>
        /// <returns>The task of the case.</returns>
        public static async Task ARemovedCardIsReportedAsGoneAsync(IRFiDGearExtensionHost host, IReaderOperator person)
        {
            var placed = await WaitForAsync(
                host,
                person,
                "Put ONE card that RFiDGear can read on the reader, and take every other card away from the reader. " +
                "The test goes on by itself when it sees the card.",
                scan => scan.State == ReaderScanState.Ready && scan.HasCard,
                "a card on the reader");
            var present = placed[placed.Count - 1];
            using var events = new EventCounter(host.Reader);

            var seen = await WaitForAsync(
                host,
                person,
                "Now take the card off the reader, and keep every other card away from it too. " +
                "The test goes on by itself when it sees the reader empty.",
                scan => scan.State == ReaderScanState.Ready && !scan.HasCard,
                "the reader to be empty (is a card still on it, or lying near it?)");

            Require(seen[seen.Count - 1].Equals(host.Reader.CurrentSnapshot), "The answer of the last scan did not become the current picture of the reader.");
            RequireEventsForChanges(events, present, seen);
        }

        /// <summary>
        /// A card that is replaced by another is reported with its own UID, and every change of the picture on the way is
        /// announced exactly once (the reader may or may not see the empty reader between the two cards, depending on how
        /// fast the person is). The case watches the reader, so nobody has to click when the physical step is done.
        /// </summary>
        /// <param name="host">A new host.</param>
        /// <param name="person">Who moves the cards.</param>
        /// <returns>The task of the case.</returns>
        public static async Task AReplacedCardIsReportedWithItsOwnUidAsync(IRFiDGearExtensionHost host, IReaderOperator person)
        {
            var placed = await WaitForAsync(
                host,
                person,
                "Put ONE card that RFiDGear can read on the reader, and take every other card away from the reader. " +
                "The test goes on by itself when it sees the card.",
                scan => scan.State == ReaderScanState.Ready && scan.HasCard,
                "a card on the reader");
            var first = placed[placed.Count - 1];
            var firstUid = first.Credentials[0].Uid;
            using var events = new EventCounter(host.Reader);

            var seen = await WaitForAsync(
                host,
                person,
                "Now replace the card with a DIFFERENT card that RFiDGear can read (another UID), and keep every other card " +
                "away. The test goes on by itself when it sees the new card.",
                scan => scan.State == ReaderScanState.Ready && scan.HasCard
                        && !string.Equals(scan.Credentials[0].Uid, firstUid, StringComparison.Ordinal),
                "a different card on the reader (the same card does not count)");

            Require(seen[seen.Count - 1].Equals(host.Reader.CurrentSnapshot), "The answer of the last scan did not become the current picture of the reader.");
            RequireEventsForChanges(events, first, seen);
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
            var placed = await WaitForAsync(
                host,
                person,
                "Put ONE MIFARE card (DESFire, Classic or Ultralight) on the reader, and take every other card away from the " +
                "reader. The test goes on by itself when it sees the card.",
                scan => scan.State == ReaderScanState.Ready && scan.HasCard,
                "a card on the reader");
            var mifare = placed[placed.Count - 1];
            var oldUid = mifare.Credentials[0].Uid;
            using var events = new EventCounter(host.Reader);

            // The reader cannot see the second card, so this step is confirmed by a click, and it must be ONE move: the
            // fix guards the read that goes straight from the MIFARE card to the other one, without an empty reader between.
            await AskAsync(
                person,
                "Now replace the card with an HF card that is NOT a MIFARE card, but that this reader can read (for example ISO " +
                "15693; iCLASS only if your reader reads it), in ONE move: do not take the MIFARE card away first, and keep " +
                "every other card away. The reader should beep or show its LED. Then click OK.");
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

        // Scans until the reader shows what the person was asked for, and returns every answer on the way (the last one is
        // the one that showed it).
        private static async Task<IReadOnlyList<ReaderSnapshot>> WaitForAsync(
            IRFiDGearExtensionHost host,
            IReaderOperator person,
            string instruction,
            Func<ReaderSnapshot, bool> reached,
            string what)
        {
            var seen = new List<ReaderSnapshot>();
            var outcome = await person.WaitAsync(
                instruction,
                async () =>
                {
                    var scan = await ScanAsync(host);
                    seen.Add(scan);

                    return reached(scan);
                },
                WaitLimit);

            if (outcome == WaitOutcome.Cancelled)
            {
                throw new XunitException("The person at the reader cancelled the test.");
            }

            if (outcome == WaitOutcome.TimedOut)
            {
                throw new XunitException(
                    $"Gave up after {WaitLimit.TotalSeconds:0} seconds waiting for {what}. " +
                    $"The last answer was: {Describe(seen.Count > 0 ? seen[seen.Count - 1] : null)}.");
            }

            return seen;
        }

        // Every change of the picture is announced once, and nothing else is: the number of events is the number of times
        // the answers of the scans differed from the one before (a scan that failed or was busy changes nothing).
        private static void RequireEventsForChanges(EventCounter events, ReaderSnapshot before, IEnumerable<ReaderSnapshot> seen)
        {
            var last = before;
            var changes = 0;
            foreach (var scan in seen)
            {
                if ((scan.State == ReaderScanState.Ready || scan.State == ReaderScanState.NoReader)
                    && !(scan.State == last.State && scan.Credentials.SequenceEqual(last.Credentials)))
                {
                    changes++;
                    last = scan;
                }
            }

            Require(changes >= 1, "The picture of the reader did not change.");
            Require(events.Count == changes, $"ReaderStateChanged was raised {events.Count} times for {changes} change(s) of the picture.");
        }

        private static string Describe(ReaderSnapshot? scan) =>
            scan == null
                ? "no scan"
                : scan.State + (scan.HasCard
                    ? ", " + string.Join(", ", scan.Credentials.Select(credential => credential.DisplayName + " " + credential.Uid))
                    : ", no card");

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
