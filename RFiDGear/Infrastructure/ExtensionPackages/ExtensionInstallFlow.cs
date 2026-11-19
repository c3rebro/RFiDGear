#nullable enable

using System;
using System.Globalization;
using System.Threading.Tasks;

namespace RFiDGear.Infrastructure.ExtensionPackages
{
    /// <summary>
    /// What the person sees and chooses while an extension package is installed. A seam: the application shows message boxes
    /// and a file dialog, the tests answer for the person.
    /// </summary>
    internal interface IExtensionInstallDialogs
    {
        /// <summary>Asks for the package that is to be installed.</summary>
        /// <returns>The file, or <c>null</c> when the person gave up.</returns>
        string? ChoosePackage();

        /// <summary>Says what is about to be installed and asks whether to go on.</summary>
        /// <param name="text">The explanation.</param>
        /// <returns><c>true</c> to install it.</returns>
        bool Confirm(string text);

        /// <summary>Tells the person what happened.</summary>
        /// <param name="text">The message.</param>
        /// <param name="isError">Whether the extension was not installed because something failed.</param>
        void Inform(string text, bool isError);

        /// <summary>Shows that the application is working until the result is disposed.</summary>
        /// <returns>The scope of the busy state.</returns>
        IDisposable Working();
    }

    /// <summary>
    /// The path a person takes to install an extension update from a package: the package, what it is and who signed it, the
    /// question whether to install it, and the result. Nothing is put in place before the person has said yes, and nothing that
    /// is running is touched: the extension is used from the next start of RFiDGear.
    /// </summary>
    internal sealed class ExtensionInstallFlow
    {
        private readonly ExtensionPackageInstaller _installer;
        private readonly IExtensionInstallDialogs _dialogs;
        private readonly Func<string, string> _text;

        /// <summary>Creates the flow.</summary>
        /// <param name="installer">The installer.</param>
        /// <param name="dialogs">What the person sees and chooses.</param>
        /// <param name="text">Gives the text of a resource key, in the language of the application.</param>
        internal ExtensionInstallFlow(ExtensionPackageInstaller installer, IExtensionInstallDialogs dialogs, Func<string, string> text)
        {
            _installer = installer ?? throw new ArgumentNullException(nameof(installer));
            _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
            _text = text ?? throw new ArgumentNullException(nameof(text));
        }

        /// <summary>Takes the person through the installation.</summary>
        /// <returns>A task that completes when the person has seen the result.</returns>
        internal async Task RunAsync()
        {
            var path = _dialogs.ChoosePackage();
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            PrepareResult prepared;
            using (_dialogs.Working())
            {
                prepared = await Task.Run(() => _installer.Prepare(path!));
            }

            if (prepared.Staged == null)
            {
                _dialogs.Inform(ProblemText(prepared.Problem), true);
                return;
            }

            var staged = prepared.Staged;
            if (!_dialogs.Confirm(ConfirmText(staged)))
            {
                _installer.Discard(staged);
                return;
            }

            ExtensionPackageProblem? problem;
            using (_dialogs.Working())
            {
                problem = await Task.Run(() => _installer.Commit(staged));
            }

            _dialogs.Inform(
                problem == null
                    ? string.Format(CultureInfo.CurrentCulture, _text("messageExtensionInstalled"), staged.Manifest.DisplayName, staged.Manifest.Version)
                    : ProblemText(problem),
                problem != null);
        }

        // What the person is told before saying yes: the extension, which version it replaces, who signed it, and that it is
        // a program. A package carries a text of its own too, which is shown as it is.
        private string ConfirmText(StagedExtension staged)
        {
            var manifest = staged.Manifest;
            var installed = staged.InstalledVersion == null
                ? _text("textExtensionNotInstalled")
                : string.Format(CultureInfo.CurrentCulture, _text("textExtensionInstalledNow"), staged.InstalledVersion);
            var signer = staged.Signer == null
                ? _text("textExtensionNotSigned")
                : string.Format(
                    CultureInfo.CurrentCulture,
                    _text(staged.Signer.SignatureValid ? "textExtensionSignedBy" : "textExtensionSignatureNotValid"),
                    staged.Signer.Name);

            return string.Format(
                CultureInfo.CurrentCulture,
                _text("messageExtensionInstallConfirm"),
                manifest.DisplayName,
                manifest.Version,
                installed,
                signer,
                string.IsNullOrWhiteSpace(manifest.Notes) ? string.Empty : Environment.NewLine + Environment.NewLine + manifest.Notes);
        }

        private string ProblemText(ExtensionPackageProblem? problem)
        {
            var key = problem switch
            {
                ExtensionPackageProblem.NotAPackage => "messageExtensionProblemNotAPackage",
                ExtensionPackageProblem.HostTooOld => "messageExtensionProblemHostTooOld",
                ExtensionPackageProblem.WrongPlatform => "messageExtensionProblemWrongPlatform",
                ExtensionPackageProblem.AlreadyInstalled => "messageExtensionProblemAlreadyInstalled",
                ExtensionPackageProblem.NotNewer => "messageExtensionProblemNotNewer",
                ExtensionPackageProblem.CannotWrite => "messageExtensionProblemCannotWrite",
                _ => "messageExtensionProblemGeneric",
            };

            // The code is for whoever supports the package; no path, file name or exception text goes into the message.
            return string.Format(CultureInfo.CurrentCulture, _text(key), problem?.ToString() ?? string.Empty);
        }
    }
}
