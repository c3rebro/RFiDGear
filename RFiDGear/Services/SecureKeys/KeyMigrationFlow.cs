#nullable enable
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RFiDGear.Services.SecureKeys
{
    /// <summary>
    /// What the person sees and chooses while the keys of a project are moved. A seam: the application shows message boxes and
    /// file dialogs, the tests answer for the person.
    /// </summary>
    internal interface IKeyMigrationDialogs
    {
        /// <summary>Explains what is about to happen and asks whether to go on.</summary>
        /// <param name="text">The explanation.</param>
        /// <returns><c>true</c> to go on.</returns>
        bool Confirm(string text);

        /// <summary>Asks for the project whose keys are to be moved.</summary>
        /// <returns>The file, or <c>null</c> when the person gave up.</returns>
        string? ChooseSource();

        /// <summary>Asks where the new project is to be saved.</summary>
        /// <param name="suggestedPath">A file name next to the old project, to start from.</param>
        /// <returns>The file, or <c>null</c> when the person gave up.</returns>
        string? ChooseTarget(string suggestedPath);

        /// <summary>Tells the person what happened.</summary>
        /// <param name="text">The message.</param>
        /// <param name="isError">Whether the keys were not moved because something failed.</param>
        void Inform(string text, bool isError);

        /// <summary>Asks whether the old project is to be deleted.</summary>
        /// <param name="text">The question.</param>
        /// <returns><c>true</c> to delete it.</returns>
        bool AskDeleteOriginal(string text);

        /// <summary>Shows that the application is working until the result is disposed.</summary>
        /// <returns>The scope of the busy state.</returns>
        IDisposable Working();
    }

    /// <summary>
    /// The path a person takes to move the keys of a project: an explanation, the old project, the new project, the result and
    /// the question whether the old project may go. Nothing is changed before the person has chosen both files.
    /// </summary>
    internal sealed class KeyMigrationFlow
    {
        // More lines than this are not listed one by one: the report of a large project would not fit a message box.
        private const int ListedMoves = 12;

        private readonly KeyMigration _migration;
        private readonly IKeyMigrationDialogs _dialogs;
        private readonly Func<string, string> _text;

        /// <summary>Creates the flow.</summary>
        /// <param name="migration">The migration.</param>
        /// <param name="dialogs">What the person sees and chooses.</param>
        /// <param name="text">Gives the text of a resource key, in the language of the application.</param>
        internal KeyMigrationFlow(KeyMigration migration, IKeyMigrationDialogs dialogs, Func<string, string> text)
        {
            _migration = migration ?? throw new ArgumentNullException(nameof(migration));
            _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
            _text = text ?? throw new ArgumentNullException(nameof(text));
        }

        /// <summary>Takes the person through the migration.</summary>
        /// <param name="cancellationToken">Cancelled when the application closes.</param>
        /// <returns>A task that completes when the person has seen the result.</returns>
        internal async Task RunAsync(CancellationToken cancellationToken = default)
        {
            if (!_dialogs.Confirm(_text("messageKeyMigrationIntro")))
            {
                return;
            }

            var source = _dialogs.ChooseSource();

            if (string.IsNullOrWhiteSpace(source))
            {
                return;
            }

            var target = _dialogs.ChooseTarget(SuggestedTarget(source));

            if (string.IsNullOrWhiteSpace(target))
            {
                return;
            }

            KeyMigrationReport report;

            using (_dialogs.Working())
            {
                report = await _migration.MoveAsync(source, target, cancellationToken).ConfigureAwait(true);
            }

            if (!report.Succeeded)
            {
                _dialogs.Inform(report.NothingToMove ? _text("messageKeyMigrationNothing") : FailureText(report), isError: !report.NothingToMove);

                return;
            }

            _dialogs.Inform(DoneText(report), isError: false);

            if (!_dialogs.AskDeleteOriginal(Format("messageKeyMigrationAskDelete", report.SourcePath)))
            {
                return;
            }

            bool deleted;

            using (_dialogs.Working())
            {
                deleted = await _migration.DeleteOriginalAsync(report, cancellationToken).ConfigureAwait(true);
            }

            _dialogs.Inform(_text(deleted ? "messageKeyMigrationDeleted" : "messageKeyMigrationNotDeleted"), isError: !deleted);
        }

        private static string SuggestedTarget(string source) =>
            Path.Combine(Path.GetDirectoryName(source) ?? string.Empty, Path.GetFileNameWithoutExtension(source) + ".secured" + Path.GetExtension(source));

        private string FailureText(KeyMigrationReport report)
        {
            var reason = report.Failure switch
            {
                KeyMigrationFailure.SameFile => Format("messageKeyMigrationSameFile"),
                KeyMigrationFailure.TargetExists => Format("messageKeyMigrationTargetExists"),
                KeyMigrationFailure.SourceUnreadable => Format("messageKeyMigrationSourceUnreadable"),
                KeyMigrationFailure.KeyStoreRefused when report.FailureDetail != 0 => Format("messageKeyMigrationKeyStoreRefused", report.FailureDetail),
                KeyMigrationFailure.KeyStoreRefused => Format("messageKeyMigrationKeyStoreUnavailable"),
                KeyMigrationFailure.ReadBackDiffers => Format("messageKeyMigrationReadBackDiffers"),
                KeyMigrationFailure.TargetNotWritten => Format("messageKeyMigrationTargetNotWritten"),
                KeyMigrationFailure.VerificationFailed => Format("messageKeyMigrationVerificationFailed"),
                KeyMigrationFailure.Cancelled => Format("messageKeyMigrationCancelled"),
                _ => Format("messageKeyMigrationUnexpected"),
            };

            // "Nothing is left" is said only when it is true: a credential or a file that could not be removed is named instead.
            var outcome = report.LeftBehind
                ? Format("messageKeyMigrationNotUndone", LeftBehindLines(report))
                : _text("messageKeyMigrationUndone");

            return Format("messageKeyMigrationFailed", reason, outcome);
        }

        private static string LeftBehindLines(KeyMigrationReport report)
        {
            var lines = new StringBuilder();

            foreach (var reference in report.UnremovedReferences)
            {
                lines.AppendLine().Append("- ").Append(WindowsCredentialKeyProvider.TargetPrefix).Append(reference);
            }

            if (report.UndeletedTarget.Length > 0)
            {
                lines.AppendLine().Append("- ").Append(report.UndeletedTarget);
            }

            return lines.ToString();
        }

        // Tasks, slots and references: nothing in it is a key.
        private string DoneText(KeyMigrationReport report)
        {
            var text = new StringBuilder(Format("messageKeyMigrationDone", report.Moved.Count, report.DifferentKeys, report.TargetPath));

            foreach (var moved in report.Moved.Take(ListedMoves))
            {
                text.AppendLine().Append(CultureInfo.CurrentCulture, $"{moved.Task}: {moved.Slot} = {moved.Reference}");
            }

            if (report.Moved.Count > ListedMoves)
            {
                text.AppendLine().Append("...");
            }

            if (report.NotMoved.Count > 0)
            {
                text.AppendLine().AppendLine().Append(Format("messageKeyMigrationNotMoved", report.NotMoved.Count));
            }

            return text.ToString();
        }

        private string Format(string resourceKey, params object[] arguments) =>
            string.Format(CultureInfo.CurrentCulture, _text(resourceKey), arguments);
    }
}
