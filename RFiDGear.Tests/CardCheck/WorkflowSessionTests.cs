using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using RFiDGear.ExtensionLibraries.CardCheck;

using Xunit;

namespace RFiDGear.Tests.CardCheck
{
    public sealed class WorkflowSessionTests
    {
        private readonly RecordingCardCheckLog _log = new RecordingCardCheckLog();

        private IWorkflowSession Session() => CardCheckServices.CreateWorkflowSession(_log);

        private static WorkflowOrder Order() => Order("1", "KP-000005", "5");

        private static WorkflowOrder Order(string id) => Order(id, "KP-000005", "5");

        private static WorkflowOrder Order(string id, string job, string chip) => new WorkflowOrder(id, job, chip);

        private static ReportAnalysis Programmable() => Programmable("1760");

        private static ReportAnalysis Programmable(string memory) =>
            new ReportAnalysis { Supported = true, Programmable = true, FreeMemoryText = memory, ChipType = "DESFire EV2" };

        private static LsmTemplate Template(string name) => LsmTemplateCatalog.All.Single(t => t.Name == name);

        [Fact]
        public void ANewSession_IsEmpty()
        {
            var session = Session();

            Assert.Same(SessionSnapshot.Empty, session.Current);
            Assert.Null(session.Current.Order);
            Assert.Null(session.Current.Findings);
            Assert.Null(session.Current.Selection);
            Assert.Null(session.Current.ChosenTemplate);
            Assert.Equal(0, session.Current.Version);
            Assert.Equal(ReportLanguage.German, session.Current.Language);
        }

        [Fact]
        public void Begin_SetsTheOrderAndTheLanguage_AndCountsAChange()
        {
            var session = Session();

            Assert.True(session.Begin(Order(), ReportLanguage.English).IsSuccess);

            Assert.Equal(Order(), session.Current.Order);
            Assert.Equal(ReportLanguage.English, session.Current.Language);
            Assert.Equal(1, session.Current.Version);
        }

        [Fact]
        public void Begin_Again_ForgetsEverythingAboutThePreviousOrder()
        {
            var session = Session();
            session.Begin(Order("1"), ReportLanguage.English);
            session.RecordFindings(Programmable());

            session.Begin(Order("2", "J2", "C2"), ReportLanguage.German);

            Assert.Equal("2", session.Current.Order!.Id);
            Assert.Equal(ReportLanguage.German, session.Current.Language);
            Assert.Null(session.Current.Findings);
            Assert.Null(session.Current.Selection);
            Assert.Null(session.Current.ChosenTemplate);
            Assert.Equal(3, session.Current.Version);
        }

        [Theory]
        [InlineData("")]
        [InlineData("  ")]
        public void Begin_WithoutAnOrderId_IsRefusedAndChangesNothing(string id)
        {
            var session = Session();
            session.Begin(Order("1"), ReportLanguage.English);
            var before = session.Current;

            var result = session.Begin(Order(id), ReportLanguage.German);

            Assert.False(result.IsSuccess);
            Assert.Equal("session.order.invalid", result.Failure!.Code);
            Assert.Same(before, session.Current);
        }

        [Fact]
        public void Begin_WithAnUndefinedLanguage_IsRefused()
        {
            var result = Session().Begin(Order(), (ReportLanguage)42);

            Assert.False(result.IsSuccess);
            Assert.Equal("session.language.invalid", result.Failure!.Code);
        }

        [Fact]
        public void RecordFindings_StoresThemWithWhatTheRulesMakeOfThem_AndPreselectsTheDefault()
        {
            var session = Session();
            session.Begin(Order(), ReportLanguage.German);

            Assert.True(session.RecordFindings(Programmable("1760")).IsSuccess);

            Assert.Equal("DESFire EV2", session.Current.Findings!.ChipType);
            Assert.Equal(ChipVerdict.Programmable, session.Current.Selection!.Verdict);
            Assert.Equal("MD4000L_AV", session.Current.ChosenTemplate!.Name);
            Assert.Equal(2, session.Current.Version);
        }

        [Fact]
        public void RecordFindings_ForAChipThatCannotBeProgrammed_ChoosesNothing()
        {
            var session = Session();
            session.Begin(Order(), ReportLanguage.German);

            session.RecordFindings(new ReportAnalysis { Supported = false, Programmable = false });

            Assert.Equal(ChipVerdict.NotSupported, session.Current.Selection!.Verdict);
            Assert.Null(session.Current.ChosenTemplate);
        }

        [Fact]
        public void RecordFindings_BeforeACheckHasBegun_IsRefusedAndChangesNothing()
        {
            var session = Session();

            var result = session.RecordFindings(Programmable());

            Assert.False(result.IsSuccess);
            Assert.Equal("session.noOrder", result.Failure!.Code);
            Assert.Same(SessionSnapshot.Empty, session.Current);
        }

        [Fact]
        public void ChooseTemplate_AcceptsOneThatFits_AndKeepsTheRest()
        {
            var session = Session();
            session.Begin(Order(), ReportLanguage.German);
            session.RecordFindings(Programmable("1760"));
            var findings = session.Current.Findings;
            var selection = session.Current.Selection;

            Assert.True(session.ChooseTemplate(Template("MD2500L_AV")).IsSuccess);

            Assert.Equal("MD2500L_AV", session.Current.ChosenTemplate!.Name);
            Assert.Same(findings, session.Current.Findings);
            Assert.Same(selection, session.Current.Selection);
        }

        [Fact]
        public void ChooseTemplate_OneThatDoesNotFit_IsRefusedAndTheChoiceStays()
        {
            var session = Session();
            session.Begin(Order(), ReportLanguage.German);
            session.RecordFindings(Programmable("1760"));

            foreach (var name in new[] { "MD10000L_AV", "MD32000L_AV", "MC1000L_AV" })
            {
                var result = session.ChooseTemplate(Template(name));

                Assert.False(result.IsSuccess);
                Assert.Equal("session.template.notAvailable", result.Failure!.Code);
            }

            Assert.Equal("MD4000L_AV", session.Current.ChosenTemplate!.Name);
        }

        [Fact]
        public void ChooseTemplate_WithoutFindingsOrWithoutAnOrder_IsRefused()
        {
            var session = Session();
            Assert.Equal("session.noOrder", session.ChooseTemplate(Template("MDBasic")).Failure!.Code);

            session.Begin(Order(), ReportLanguage.German);
            Assert.Equal("session.template.notAvailable", session.ChooseTemplate(Template("MDBasic")).Failure!.Code);
        }

        [Fact]
        public void Reset_ForgetsTheOrderToo_ButTheVersionKeepsCounting()
        {
            var session = Session();
            session.Begin(Order(), ReportLanguage.English);
            session.RecordFindings(Programmable());

            session.Reset();

            Assert.Null(session.Current.Order);
            Assert.Null(session.Current.Findings);
            Assert.Equal(ReportLanguage.German, session.Current.Language);
            Assert.Equal(3, session.Current.Version);
        }

        [Fact]
        public void Current_IsTheSameSnapshotUntilSomethingChanges()
        {
            var session = Session();
            session.Begin(Order(), ReportLanguage.German);

            var first = session.Current;

            Assert.Same(first, session.Current);
            session.RecordFindings(Programmable());
            Assert.NotSame(first, session.Current);
            Assert.Null(first.Findings);
        }

        // ── the event ───────────────────────────────────────────────────────

        [Fact]
        public void Changed_IsRaisedOncePerChange_WithTheNewSnapshot_AndNeverForARefusedOne()
        {
            var session = Session();
            var seen = new List<SessionSnapshot>();
            session.Changed += (_, snapshot) => seen.Add(snapshot);

            session.RecordFindings(Programmable());
            session.Begin(Order(), ReportLanguage.German);
            session.Begin(Order(" "), ReportLanguage.German);
            session.RecordFindings(Programmable());
            session.ChooseTemplate(Template("MD10000L_AV"));
            session.ChooseTemplate(Template("MDBasic"));
            session.Reset();

            Assert.Equal(new long[] { 1, 2, 3, 4 }, seen.Select(s => s.Version));
            Assert.Same(session.Current, seen.Last());
            Assert.Equal("MDBasic", seen[2].ChosenTemplate!.Name);
        }

        // Another thread can only read the session if the lock is not held while the event is raised. (The thread that
        // raises the event could: a lock can be taken again by its owner.) Blocking is the point here, so it is kept
        // out of the test method itself.
        private static bool CanBeReadFromAnotherThread(IWorkflowSession session) =>
            Task.Run(() => session.Current.Version).Wait(TimeSpan.FromSeconds(5));

        [Fact]
        public async Task Changed_AHandlerMayReadAndChangeTheSessionAgain_WithoutADeadlock()
        {
            var session = Session();
            session.Begin(Order(), ReportLanguage.German);
            var versions = new List<long>();
            var readFromAnotherThread = true;
            session.Changed += (_, snapshot) =>
            {
                versions.Add(snapshot.Version);
                readFromAnotherThread &= CanBeReadFromAnotherThread(session);

                if (snapshot.Findings != null && snapshot.ChosenTemplate!.Name != "MDBasic")
                {
                    Assert.Equal(snapshot.Version, session.Current.Version);
                    session.ChooseTemplate(Template("MDBasic"));
                }
            };

            var change = Task.Run(() => session.RecordFindings(Programmable()));
            var finished = await Task.WhenAny(change, Task.Delay(TimeSpan.FromSeconds(30))) == change;

            Assert.True(finished, "the session deadlocked");
            Assert.True(readFromAnotherThread, "the session was locked while its event was raised");
            Assert.Equal("MDBasic", session.Current.ChosenTemplate!.Name);
            Assert.Contains(3L, versions);
        }

        [Fact]
        public void Changed_AHandlerThatThrows_DoesNotUndoTheChangeOrStopTheOthers_AndIsLoggedWithoutItsMessage()
        {
            var session = Session();
            var second = 0;
            session.Changed += (_, _) => throw new InvalidOperationException("SECRET-MESSAGE-FROM-A-HANDLER");
            session.Changed += (_, _) => second++;

            var result = session.Begin(Order(), ReportLanguage.English);

            Assert.True(result.IsSuccess);
            Assert.Equal(1, second);
            Assert.Equal(1, session.Current.Version);
            Assert.Contains("session.changed", _log.Everything, StringComparison.Ordinal);
            Assert.Contains(nameof(InvalidOperationException), _log.Everything, StringComparison.Ordinal);
            Assert.DoesNotContain("SECRET", _log.Everything, StringComparison.Ordinal);
        }

        // ── one session per window ──────────────────────────────────────────

        [Fact]
        public void TwoSessions_ShareNothing()
        {
            var one = Session();
            var two = Session();

            one.Begin(Order("1", "J1", "C1"), ReportLanguage.English);
            one.RecordFindings(Programmable("7168"));
            two.Begin(Order("2", "J2", "C2"), ReportLanguage.German);

            Assert.Equal("1", one.Current.Order!.Id);
            Assert.Equal("2", two.Current.Order!.Id);
            Assert.NotNull(one.Current.Findings);
            Assert.Null(two.Current.Findings);
            Assert.Equal(ReportLanguage.English, one.Current.Language);
            Assert.Equal(ReportLanguage.German, two.Current.Language);
        }

        [Fact]
        public async Task ManySessionsUsedAtTheSameTime_EachEndsWithItsOwnOrder()
        {
            var sessions = Enumerable.Range(0, 8).Select(_ => Session()).ToArray();

            await Task.WhenAll(sessions.Select((session, index) => Task.Run(() =>
            {
                for (var i = 0; i < 200; i++)
                {
                    session.Begin(Order("order-" + index, "J" + index, "C" + index), ReportLanguage.German);
                    session.RecordFindings(Programmable());
                    session.ChooseTemplate(Template("MDBasic"));
                }
            })));

            for (var index = 0; index < sessions.Length; index++)
            {
                Assert.Equal("order-" + index, sessions[index].Current.Order!.Id);
                Assert.Equal(600, sessions[index].Current.Version);
            }
        }

        [Fact]
        public async Task OneSessionUsedFromManyThreads_NeverShowsAnInconsistentSnapshot()
        {
            var session = Session();
            session.Begin(Order(), ReportLanguage.German);
            using var stop = new CancellationTokenSource();
            var inconsistent = new List<string>();

            var readers = Enumerable.Range(0, 3).Select(_ => Task.Run(() =>
            {
                while (!stop.IsCancellationRequested)
                {
                    var s = session.Current;

                    if ((s.Findings == null) != (s.Selection == null))
                    {
                        lock (inconsistent) { inconsistent.Add("findings and selection disagree"); }
                    }

                    if (s.ChosenTemplate != null && (s.Selection == null || !s.Selection.Available.Any(t => t.Name == s.ChosenTemplate.Name)))
                    {
                        lock (inconsistent) { inconsistent.Add("chosen template is not available"); }
                    }
                }
            })).ToArray();

            var writers = Enumerable.Range(0, 3).Select(w => Task.Run(() =>
            {
                for (var i = 0; i < 300; i++)
                {
                    // Bit operations rather than "%": the loop counter is never negative.
                    switch (i & 3)
                    {
                        case 0: session.Begin(Order(), ReportLanguage.German); break;
                        case 1: session.RecordFindings(Programmable((i & 4) == 0 ? "1760" : "300")); break;
                        case 2: session.ChooseTemplate(Template("MDBasic")); break;
                        default: session.Reset(); break;
                    }
                }
            })).ToArray();

            await Task.WhenAll(writers);
            stop.Cancel();
            await Task.WhenAll(readers);

            Assert.Empty(inconsistent);
        }

        [Fact]
        public void Arguments_AreChecked()
        {
            var session = Session();

            Assert.Throws<ArgumentNullException>(() => CardCheckServices.CreateWorkflowSession(null!));
            Assert.Throws<ArgumentNullException>(() => session.Begin(null!, ReportLanguage.German));
            Assert.Throws<ArgumentNullException>(() => session.RecordFindings(null!));
            Assert.Throws<ArgumentNullException>(() => session.ChooseTemplate(null!));
        }
    }
}
