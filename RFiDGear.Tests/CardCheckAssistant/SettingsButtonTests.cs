using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

using RFiDGear.Extensions.CardCheckAssistant.Localization;
using RFiDGear.Extensions.CardCheckAssistant.Services;
using RFiDGear.Extensions.CardCheckAssistant.ViewModels;

using Xunit;

using static RFiDGear.Tests.CardCheckAssistant.ViewModelTestSupport;

namespace RFiDGear.Tests.CardCheckAssistant
{
    /// <summary>The "Settings..." button of the page: when it can be used, and what the page does when the settings were saved.</summary>
    public class SettingsButtonTests
    {
        private sealed class FakeSettingsDialog : ISettingsDialog
        {
            private readonly List<LocalizedStrings> _shown = new List<LocalizedStrings>();
            private Action? _whileOpen;
            private bool _answer;

            public IReadOnlyList<LocalizedStrings> Shown => _shown;

            /// <summary>Sets whether the next window reports "saved", and what happens while it is open.</summary>
            public void WillAnswer(bool saved, Action? whileOpen = null)
            {
                _answer = saved;
                _whileOpen = whileOpen;
            }

            public bool Show(LocalizedStrings strings)
            {
                _shown.Add(strings);
                _whileOpen?.Invoke();

                return _answer;
            }
        }

        private sealed class CountingSettings : IAssistantSettings
        {
            private int _loads;

            public AssistantDefaults Defaults { get; set; } = AssistantDefaults.None;

            public int Loads => Volatile.Read(ref _loads);

            public AssistantDefaults Load()
            {
                Interlocked.Increment(ref _loads);

                return Defaults;
            }

            public void RememberProjectFolder(string folder) => Defaults = Defaults with { LastProjectFolder = folder };
        }

        private static CardCheckAssistantViewModel Create(FakeExtensionHost host, CountingSettings settings, FakeSettingsDialog? dialog) =>
            new CardCheckAssistantViewModel(host, Dispatcher.CurrentDispatcher, new FakeFileDialogService(), null, settings, null, dialog);

        [Fact]
        public async Task WhenTheSettingsWereSaved_ThePageReadsWhatItStartsFromAgain_ButNeverOverwritesAProject()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var settings = new CountingSettings();
                var dialog = new FakeSettingsDialog();
                using var viewModel = Create(new FakeExtensionHost(), settings, dialog);
                await viewModel.Open();
                Assert.Equal(1, settings.Loads);
                Assert.Equal(string.Empty, viewModel.ProjectPath);

                settings.Defaults = new AssistantDefaults("default.rfPrj", string.Empty);
                dialog.WillAnswer(true);
                await viewModel.OpenSettingsCommand.ExecuteAsync();

                Assert.Same(viewModel.Strings, Assert.Single(dialog.Shown));
                Assert.Equal(2, settings.Loads);
                Assert.Equal("default.rfPrj", viewModel.ProjectPath);

                // A project that is there, typed or prefilled, is the operator's now.
                settings.Defaults = new AssistantDefaults("another.rfPrj", string.Empty);
                await viewModel.OpenSettingsCommand.ExecuteAsync();

                Assert.Equal(3, settings.Loads);
                Assert.Equal("default.rfPrj", viewModel.ProjectPath);
            });
        }

        [Fact]
        public async Task WhenTheSettingsWereNotSaved_ThePageReadsNothingAgain()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var settings = new CountingSettings { Defaults = new AssistantDefaults("default.rfPrj", string.Empty) };
                var dialog = new FakeSettingsDialog();
                using var viewModel = Create(new FakeExtensionHost(), settings, dialog);
                await viewModel.Open();
                viewModel.ProjectPath = string.Empty;
                dialog.WillAnswer(false);

                await viewModel.OpenSettingsCommand.ExecuteAsync();

                Assert.Single(dialog.Shown);
                Assert.Equal(1, settings.Loads);
                Assert.Equal(string.Empty, viewModel.ProjectPath);
            });
        }

        [Fact]
        public async Task TheButton_NeedsAWindowToOpen_AnIdlePage_AndCannotBePressedTwice()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var host = new FakeExtensionHost();
                var dialog = new FakeSettingsDialog();
                using var without = Create(host, new CountingSettings(), null);
                using var viewModel = Create(host, new CountingSettings(), dialog);

                Assert.False(without.HasSettings);
                Assert.False(without.OpenSettingsCommand.CanExecute(null));
                Assert.True(viewModel.HasSettings);
                Assert.True(viewModel.OpenSettingsCommand.CanExecute(null));

                // Not while RFiDGear runs tasks, whoever started them.
                host.ExecutionHost.SetRunning(true);
                await Flush();
                Assert.False(viewModel.OpenSettingsCommand.CanExecute(null));
                host.ExecutionHost.SetRunning(false);
                await Flush();
                Assert.True(viewModel.OpenSettingsCommand.CanExecute(null));

                // And not a second window while the first is open.
                var whileOpen = new List<bool>();
                dialog.WillAnswer(false, () => whileOpen.Add(viewModel.OpenSettingsCommand.CanExecute(null)));
                await viewModel.OpenSettingsCommand.ExecuteAsync();

                Assert.Equal(new[] { false }, whileOpen);
                Assert.True(viewModel.OpenSettingsCommand.CanExecute(null));
            });
        }

        [Fact]
        public async Task WhenThePageIsClosedWhileTheWindowIsOpen_NothingIsReadAfterwards()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var settings = new CountingSettings();
                var dialog = new FakeSettingsDialog();
                var viewModel = Create(new FakeExtensionHost(), settings, dialog);
                await viewModel.Open();
                settings.Defaults = new AssistantDefaults("default.rfPrj", string.Empty);
                dialog.WillAnswer(true, viewModel.Dispose);

                await viewModel.OpenSettingsCommand.ExecuteAsync();

                Assert.True(viewModel.IsDisposed);
                Assert.Equal(1, settings.Loads);
                Assert.Equal(string.Empty, viewModel.ProjectPath);
            });
        }
    }
}
