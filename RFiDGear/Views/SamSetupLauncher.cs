#nullable enable
using System;
using System.Linq;
using System.Windows;

using RFiDGear.Contracts;
using RFiDGear.ExtensionSdk;
using RFiDGear.Infrastructure;
using RFiDGear.Infrastructure.ExtensionHost;
using RFiDGear.Infrastructure.ReaderProviders;
using RFiDGear.Services.Sam;
using RFiDGear.Services.SecureKeys;

namespace RFiDGear.View
{
    /// <summary>
    /// Shows the SAM setup of the menu as a modal window of the main window, on the real PC/SC readers (through
    /// LibLogicalAccess), the SAM sockets of a connected TWN4 and the real Credential Manager. It holds the gate of the reader while the window is open.
    /// </summary>
    internal sealed class WpfSamSetupLauncher : ISamSetupLauncher
    {
        /// <inheritdoc />
        public void Show(Window? owner, ReaderAccessGate gate, ExtensionOperationCoordinator coordinator, Func<bool?> isReaderBusy, Action<bool?> setBusy)
        {
            if (!SamSetupGate.RunExclusive(gate, coordinator, isReaderBusy, setBusy, () => ShowWindow(owner)))
            {
                var text = ResourceLoader.GetResource("messageSamSetupReaderBusy");
                var caption = ResourceLoader.GetResource("windowTitleSamSetup");

                if (owner != null)
                {
                    MessageBox.Show(owner, text, caption, MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show(text, caption, MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }

        // The SAM sockets of a TWN4 are looked at only while the Elatec reader is the one in use and connected: there is no second reader object
        // for the window to open. A PC/SC reader is always looked at.
        private static ITwn4SamRelayPorts? ConnectedTwn4Ports()
        {
            try
            {
                return ReaderDevice.Reader == ReaderTypes.Elatec && ReaderDevice.Instance is ElatecNetProvider elatec ? elatec.CreateTwn4Ports() : null;
            }
            catch (Exception e)
            {
                Serilog.Log.ForContext<WpfSamSetupLauncher>().Warning("SAM setup: the Elatec reader could not be asked for its SAM sockets ({ExceptionType}).", e.GetType().Name);

                return null;
            }
        }

        private static void ShowWindow(Window? owner)
        {
            var keyStore = new WindowsCredentialKeyProvider(new NativeCredentialStore());

            // A stored key is asked of a registry, like every key of the application: a worker thread and a time limit. The window needs
            // only the Credential Manager, so the registry holds that one provider (the same that lists the names).
            var registry = SecureKeyRegistry.Create(
                new[] { new SecureKeyProviderRegistration(WindowsCredentialKeyProvider.ProviderId, WindowsCredentialKeyProvider.DisplayName, keyStore) },
                Array.Empty<Lazy<ISecureKeyProvider, ISecureKeyProviderMetadata>>());
            var dialogs = new WpfSamSetupDialogs(() => Application.Current?.Windows.OfType<Window>().FirstOrDefault(candidate => candidate.IsActive) ?? owner);
            var service = new SamSetupService(
                SamFinders.Create(new LlaSamReaderSource(), ConnectedTwn4Ports()),
                channel => new Av3HostSession(channel),
                dialogs,
                new SamKeyReader(registry));

            using var viewModel = SamSetupViewModel.Create(service, keyStore);

            new SamSetupView(viewModel) { Owner = owner }.ShowDialog();
        }
    }

    /// <summary>The question of the SAM setup, asked with a message box. No is the default.</summary>
    internal sealed class WpfSamSetupDialogs : ISamSetupDialogs
    {
        private readonly Func<Window?> _owner;

        /// <summary>Creates the dialogs.</summary>
        /// <param name="owner">Gets the window that the dialogs belong to; may give <see langword="null"/>.</param>
        internal WpfSamSetupDialogs(Func<Window?> owner)
        {
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        }

        /// <inheritdoc />
        public bool ConfirmActivate()
        {
            var text = ResourceLoader.GetResource("messageSamSetupConfirmActivate");
            var caption = ResourceLoader.GetResource("windowTitleSamSetup");
            var owner = _owner();

            var answer = owner != null
                ? MessageBox.Show(owner, text, caption, MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No)
                : MessageBox.Show(text, caption, MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);

            return answer == MessageBoxResult.Yes;
        }
    }
}
