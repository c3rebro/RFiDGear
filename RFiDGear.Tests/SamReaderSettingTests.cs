#nullable enable
using RFiDGear.Infrastructure;
using RFiDGear.Infrastructure.FileAccess;
using RFiDGear.Infrastructure.ReaderProviders;
using RFiDGear.Models;
using RFiDGear.View;
using RFiDGear.UI.MVVMDialogs.ViewModels.Interfaces;
using RFiDGear.ViewModel;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

using Xunit;
using static RFiDGear.Tests.RenderedTab;

namespace RFiDGear.Tests
{
    /// <summary>The reader that holds the MIFARE SAM AV3: a setting, empty for none, that an old settings file does not have.</summary>
    [Collection("SettingsFileAccess")]
    public class SamReaderSettingsTests
    {
        private const string SamReader = "HID Global OMNIKEY 5422 Smartcard Reader 0";

        private static string NewTempRoot()
        {
            var root = Path.Combine(Path.GetTempPath(), "RFiDGear.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);

            return root;
        }

        [Fact]
        public void There_is_no_sam_reader_by_default_also_in_the_runtime_defaults()
        {
            Assert.Equal(string.Empty, new DefaultSpecificationRuntimeDefaults().SamReaderName);
            Assert.Equal(string.Empty, new DefaultSpecification().SamReaderName);
            Assert.Equal(string.Empty, new DefaultSpecification(true).SamReaderName);
        }

        [Fact]
        public void The_sam_reader_survives_being_saved_and_read()
        {
            var root = NewTempRoot();
            var path = Path.Combine(root, "settings.xml");

            using var writer = new SettingsReaderWriter(root, loadSettings: false);
            writer.SaveSettings(new DefaultSpecification(true) { SamReaderName = SamReader }, path);

            Assert.Equal(SamReader, writer.ReadSettings(path).SamReaderName);

            writer.SaveSettings(new DefaultSpecification(true) { SamReaderName = string.Empty }, path);

            Assert.Equal(string.Empty, writer.ReadSettings(path).SamReaderName);
        }

        [Fact]
        public void A_settings_file_from_before_the_setting_existed_reads_as_no_sam_reader()
        {
            var root = NewTempRoot();
            var path = Path.Combine(root, "settings.xml");

            using var writer = new SettingsReaderWriter(root, loadSettings: false);
            writer.SaveSettings(new DefaultSpecification(true) { SamReaderName = SamReader }, path);

            // An older RFiDGear wrote no such element.
            var withoutElement = string.Join(
                Environment.NewLine,
                File.ReadAllLines(path).Where(line => !line.Contains("SamReaderName", StringComparison.Ordinal)));
            File.WriteAllText(path, withoutElement);

            Assert.DoesNotContain("SamReaderName", File.ReadAllText(path), StringComparison.Ordinal);
            Assert.Equal(string.Empty, writer.ReadSettings(path).SamReaderName);
        }

        [Fact]
        public void An_empty_value_is_never_null()
        {
            var settings = new DefaultSpecification { SamReaderName = null! };

            Assert.Equal(string.Empty, settings.SamReaderName);
        }

        [Fact]
        public void The_sample_of_the_runtime_defaults_names_the_setting()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);

            while (directory != null && !File.Exists(Path.Combine(directory.FullName, "runtime-defaults.sample.json")))
            {
                directory = directory.Parent;
            }

            Assert.NotNull(directory);
            Assert.Contains("\"SamReaderName\"", File.ReadAllText(Path.Combine(directory!.FullName, "runtime-defaults.sample.json")), StringComparison.Ordinal);
        }
    }

    /// <summary>Which PC/SC reader is the reader of the card, with a SAM reader next to it: never the library's first reader, never the SAM's.</summary>
    public class PcscReaderChoiceTests
    {
        private const string Contact = "HID Global OMNIKEY 5422 Smartcard Reader 0";
        private const string Contactless = "HID Global OMNIKEY 5422CL Smartcard Reader 0";

        private static readonly string[] Names = { Contact, Contactless };

        [Fact]
        public void A_chosen_reader_that_exists_is_the_reader_of_the_card_when_no_sam_is_configured()
        {
            var pick = PcscReaderChoice.Pick(Names, Contactless, string.Empty);

            Assert.Equal(PcscReaderPick.Named, pick.Kind);
            Assert.Equal(Contactless, pick.Name);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("a reader that is gone")]
        public void Without_a_usable_choice_and_without_a_sam_the_library_picks_as_before(string? selected)
        {
            Assert.Equal(PcscReaderPick.Default, PcscReaderChoice.Pick(Names, selected, null).Kind);
            Assert.Equal(PcscReaderPick.Default, PcscReaderChoice.Pick(Names, selected, "  ").Kind);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("a reader that is gone")]
        public void With_a_sam_reader_the_library_is_never_asked_to_pick_a_reader_for_the_card(string? selected)
        {
            // The library's default reader unit takes the first reader, which on this pair is the contact slot with the SAM in it.
            var pick = PcscReaderChoice.Pick(Names, selected, Contact);

            Assert.Equal(PcscReaderPick.None, pick.Kind);
            Assert.Null(pick.Name);
        }

        [Theory]
        [InlineData(Contact)]
        [InlineData("hid global omnikey 5422 smartcard reader 0")]
        [InlineData("  " + Contact + "  ")]
        public void The_reader_of_the_sam_is_never_the_reader_of_the_card(string samName)
        {
            Assert.Equal(PcscReaderPick.None, PcscReaderChoice.Pick(Names, Contact, samName).Kind);
        }

        [Fact]
        public void Another_reader_than_the_sam_is_the_reader_of_the_card()
        {
            var pick = PcscReaderChoice.Pick(Names, Contactless, Contact);

            Assert.Equal(PcscReaderPick.Named, pick.Kind);
            Assert.Equal(Contactless, pick.Name);
        }

        [Theory]
        [InlineData(null, null, false)]
        [InlineData("", "", false)]
        [InlineData(Contact, "", false)]
        [InlineData("", Contact, false)]
        [InlineData(Contact, Contactless, false)]
        [InlineData(Contact, Contact, true)]
        [InlineData(Contact, "HID GLOBAL OMNIKEY 5422 SMARTCARD READER 0 ", true)]
        public void Two_names_are_the_same_reader_when_they_differ_only_in_case_and_the_spaces_around_them(string? a, string? b, bool same)
        {
            Assert.Equal(same, PcscReaderChoice.IsSamReader(a, b));
        }

        [Theory]
        [InlineData(true, "chosen", "saved", "chosen")]
        [InlineData(true, "", "saved", "")]
        [InlineData(true, null, "saved", "")]
        [InlineData(false, "chosen", "saved", "saved")]
        [InlineData(false, "chosen", null, "")]
        public void The_dialog_saves_the_sam_reader_only_for_the_provider_that_shows_it(bool pcsc, string? chosen, string? saved, string expected)
        {
            Assert.Equal(expected, PcscReaderChoice.SamReaderToSave(pcsc, chosen, saved));
        }
    }

    /// <summary>The reader settings dialog: the SAM reader is loaded, kept, shown for the PC/SC provider only, and a SAM reader that is the reader of the card is said to be wrong.</summary>
    [Collection("WpfBindingTrace")]
    public class SamReaderSetupTests
    {
        private const string SamReader = "HID Global OMNIKEY 5422 Smartcard Reader 0";

        [Fact]
        public async Task The_saved_sam_reader_is_loaded_and_offered_even_when_the_provider_shows_no_readers()
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                var directory = Directory.CreateTempSubdirectory("SamReaderSetup").FullName;

                try
                {
                    using var settings = new SettingsReaderWriter(directory, loadSettings: false);
                    settings.DefaultSpecification.DefaultReaderProvider = ReaderTypes.Elatec;
                    settings.DefaultSpecification.SamReaderName = SamReader;

                    var viewModel = new SetupViewModel(null!, settings, new ObservableCollection<IDialogViewModel>());

                    Assert.Equal(SamReader, viewModel.SelectedSamReaderName);

                    // Not shown for another provider: only "none" is offered, and the saved name is not lost.
                    Assert.Equal(new[] { string.Empty }, viewModel.AvailableSamReaders.Select(item => item.Name));
                    Assert.False(viewModel.IsPcscReaderSelected);
                }
                finally
                {
                    Directory.Delete(directory, true);
                }

                return Task.CompletedTask;
            });
        }

        [Fact]
        public async Task A_sam_reader_that_is_the_reader_of_the_card_is_a_warning_and_another_one_is_not()
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                var directory = Directory.CreateTempSubdirectory("SamReaderSetup").FullName;

                try
                {
                    using var settings = new SettingsReaderWriter(directory, loadSettings: false);
                    settings.DefaultSpecification.DefaultReaderProvider = ReaderTypes.Elatec;

                    var viewModel = new SetupViewModel(null!, settings, new ObservableCollection<IDialogViewModel>());
                    var changed = new List<string?>();
                    viewModel.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

                    viewModel.SelectedReaderName = SamReader;
                    viewModel.SelectedSamReaderName = "another reader";

                    Assert.False(viewModel.SamReaderIsCardReader);
                    Assert.Equal(string.Empty, viewModel.SamReaderWarningText);

                    viewModel.SelectedSamReaderName = SamReader.ToLowerInvariant();

                    Assert.True(viewModel.SamReaderIsCardReader);
                    Assert.Equal(ResourceLoader.GetResource("labelSamReaderIsCardReader"), viewModel.SamReaderWarningText);
                    Assert.Contains(nameof(SetupViewModel.SamReaderWarningText), changed);
                }
                finally
                {
                    Directory.Delete(directory, true);
                }

                return Task.CompletedTask;
            });
        }

        private sealed class StubTab
        {
            public string LocalizationResourceSet { get; } = string.Empty;

            public bool IsPcscReaderSelected { get; set; }

            public bool IsRdpSession => false;

            public string RdpWarningText => string.Empty;

            public IReadOnlyList<SamReaderItem> AvailableSamReaders { get; } = new[] { new SamReaderItem(string.Empty, "(none)"), new SamReaderItem(SamReader, SamReader) };

            public string SelectedSamReaderName { get; set; } = SamReader;

            public string SamReaderWarningText { get; set; } = string.Empty;
        }

        [Theory]
        [InlineData(true, Visibility.Visible)]
        [InlineData(false, Visibility.Collapsed)]
        public async Task The_sam_reader_box_is_shown_for_the_pcsc_provider_only_with_the_saved_reader_chosen(bool pcsc, Visibility expected)
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var tab = new TabPageReaderSettingsView { DataContext = new StubTab { IsPcscReaderSelected = pcsc } };
                var window = new Window { Content = tab, Width = 600, Height = 400, ShowActivated = false, ShowInTaskbar = false };

                try
                {
                    window.Show();
                    window.UpdateLayout();
                    await Flush();
                    window.UpdateLayout();

                    var box = LogicalDescendants(tab).OfType<ComboBox>().Single(candidate => candidate.Name == "SamReaderBox");

                    Assert.Equal(expected, box.Parent is StackPanel panel ? panel.Visibility : Visibility.Collapsed);
                    Assert.Equal(new[] { string.Empty, SamReader }, box.Items.Cast<SamReaderItem>().Select(item => item.Name));
                    Assert.Equal(SamReader, box.SelectedValue);
                }
                finally
                {
                    window.Close();
                }
            });
        }
    }
}