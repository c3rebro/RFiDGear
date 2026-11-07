using System;
using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

using RFiDGear.Extensions.CardCheckAssistant.Localization;
using RFiDGear.Extensions.CardCheckAssistant.Services;
using RFiDGear.Extensions.CardCheckAssistant.ViewModels;
using RFiDGear.Extensions.CardCheckAssistant.Views;

using Xunit;

using static RFiDGear.Tests.CardCheckAssistant.ViewModelTestSupport;

using Keys = RFiDGear.Extensions.CardCheckAssistant.Localization.ExtensionStrings.Keys;

namespace RFiDGear.Tests.CardCheckAssistant
{
    /// <summary>
    /// The settings window around the view: it closes when the view model asks, it asks the view model before it lets a
    /// close through (so the cross and Alt+F4 do not lose changes without asking), and it tells the page whether the
    /// settings were saved.
    /// </summary>
    public class SettingsDialogTests
    {
        /// <summary>
        /// Shows the window and lets a test drive it from inside the modal loop. A test that fails, or never closes the
        /// window, ends the window anyway (answering yes to any question) and has the failure thrown afterwards.
        /// </summary>
        private static bool ShowAndDrive(
            SettingsDialog dialog,
            LocalizedStrings strings,
            SettingsViewModelTests.FakeSettingsPrompts prompts,
            Func<Window, SettingsViewModel, Task> drive)
        {
            Exception? failure = null;

            var saved = dialog.Show(strings, (window, viewModel) =>
            {
                var watchdog = new DispatcherTimer { Interval = TimeSpan.FromSeconds(20) };

                void End()
                {
                    // A save that is still going makes the view model refuse to close; a view model that is disposed no longer does.
                    viewModel.Dispose();
                    prompts.WillAnswer(true);
                    try
                    {
                        window.Close();
                    }
                    catch (InvalidOperationException)
                    {
                        // The window is already closing.
                    }
                }

                watchdog.Tick += (_, _) =>
                {
                    watchdog.Stop();
                    failure ??= new TimeoutException("The settings window was still open when the test expected it to be closed.");
                    End();
                };

                // It runs until the window is gone, also after the test has done what it wanted: a window that should have
                // closed and did not would otherwise keep this modal loop, and with it the whole test run, waiting forever.
                window.Closed += (_, _) => watchdog.Stop();
                watchdog.Start();

                _ = Drive();

                async Task Drive()
                {
                    try
                    {
                        await WaitUntil(() => viewModel.InputsEnabled, "the settings to load");
                        await drive(window, viewModel);
                    }
                    catch (Exception ex)
                    {
                        failure ??= ex;
                        End();
                    }
                    finally
                    {
                        // Whatever the test wanted to do is done: a window that is still open now has a moment to close.
                        if (window.IsVisible)
                        {
                            watchdog.Stop();
                            watchdog.Interval = TimeSpan.FromSeconds(3);
                            watchdog.Start();
                        }
                    }
                }
            });

            if (failure != null)
            {
                ExceptionDispatchInfo.Capture(failure).Throw();
            }

            return saved;
        }

        private static (SettingsDialog Dialog, SettingsViewModelTests.FakeSettingsEditor Editor, SettingsViewModelTests.FakeSettingsPrompts Prompts, LocalizedStrings Strings) Create()
        {
            // Nothing is entered, so the checks of a save find nothing to complain about and touch no disk.
            var editor = new SettingsViewModelTests.FakeSettingsEditor { LoadResult = new SettingsEditorLoad(new SettingsData(), SettingsProblem.None) };
            var prompts = new SettingsViewModelTests.FakeSettingsPrompts();

            return (new SettingsDialog(editor, new FakeFileDialogService(), prompts), editor, prompts, new LocalizedStrings("en-US"));
        }

        [Fact]
        public async Task Saving_ClosesTheWindow_AndTheDialogSaysTheSettingsWereSaved()
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                var (dialog, editor, prompts, strings) = Create();
                using var editorDisposal = editor;

                var saved = ShowAndDrive(dialog, strings, prompts, async (window, viewModel) =>
                {
                    Assert.Equal(strings[Keys.SettingsTitle], window.Title);
                    Assert.True(window.IsVisible);

                    viewModel.OneFolderPerJob = true;
                    await viewModel.OkCommand.ExecuteAsync();
                });

                Assert.True(saved);
                Assert.Single(editor.Saves);
                Assert.Empty(prompts.Asked);
            });
        }

        [Fact]
        public async Task AWindowThatCannotCloseBecauseASaveIsPending_FailsTheTest_InsteadOfHangingIt()
        {
            // The harness itself: while the settings are being saved the view model refuses every close, and the watchdog
            // has to get through anyway. The thread is a background thread, so even a window that stays open for good
            // ends this test after 30 seconds and does not hold up the run.
            var run = StaTestRunner.RunOnStaThreadAsync(() =>
            {
                var (dialog, editor, prompts, strings) = Create();
                using var editorDisposal = editor;
                editor.HoldSave();

                var clock = Stopwatch.StartNew();
                var failure = Assert.Throws<TimeoutException>(() => ShowAndDrive(dialog, strings, prompts, (shown, viewModel) =>
                {
                    viewModel.OneFolderPerJob = true;
                    _ = viewModel.OkCommand.ExecuteAsync();

                    return Task.CompletedTask;
                }));
                clock.Stop();

                // The watchdog waits three seconds. The held save gives up after ten and would then close the window the
                // usual way, so a window closed by the watchdog is one that is closed well before that.
                Assert.Contains("still open", failure.Message, StringComparison.Ordinal);
                Assert.True(clock.Elapsed < TimeSpan.FromSeconds(8), "The watchdog needed " + clock.Elapsed.TotalSeconds + " seconds to close the window.");
                editor.ReleaseSave();
            });

            Assert.Same(run, await Task.WhenAny(run, Task.Delay(TimeSpan.FromSeconds(30))));
            await run;
        }

        [Fact]
        public async Task Cancelling_AfterAChange_AsksFirst_AndTheWindowOnlyClosesOnYes()
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                var (dialog, editor, prompts, strings) = Create();
                using var editorDisposal = editor;

                var saved = ShowAndDrive(dialog, strings, prompts, async (window, viewModel) =>
                {
                    viewModel.OneFolderPerJob = true;
                    prompts.WillAnswer(false);

                    viewModel.CancelCommand.Execute(null);
                    await Flush();

                    Assert.True(window.IsVisible);
                    Assert.Single(prompts.Asked);

                    prompts.WillAnswer(true);
                    viewModel.CancelCommand.Execute(null);
                });

                Assert.False(saved);
                Assert.Equal(2, prompts.Asked.Count);
                Assert.Empty(editor.Saves);
            });
        }

        [Fact]
        public async Task TheCross_WithoutChanges_ClosesWithoutAsking_AndWithChanges_AsksToo()
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                var (dialog, editor, prompts, strings) = Create();
                using var editorDisposal = editor;

                var savedWithoutChanges = ShowAndDrive(dialog, strings, prompts, (window, _) =>
                {
                    window.Close();

                    return Task.CompletedTask;
                });

                Assert.False(savedWithoutChanges);
                Assert.Empty(prompts.Asked);

                var savedWithChanges = ShowAndDrive(dialog, strings, prompts, async (window, viewModel) =>
                {
                    viewModel.RemoveTemporaryReports = true;
                    prompts.WillAnswer(false);

                    window.Close();
                    await Flush();

                    Assert.True(window.IsVisible);
                    Assert.Single(prompts.Asked);

                    prompts.WillAnswer(true);
                    window.Close();
                });

                Assert.False(savedWithChanges);
                Assert.Equal(2, prompts.Asked.Count);
            });
        }
    }
}
