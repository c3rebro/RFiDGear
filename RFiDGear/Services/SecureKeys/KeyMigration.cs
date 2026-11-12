#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using RFiDGear.Contracts;
using RFiDGear.Models;
using Serilog;

namespace RFiDGear.Services.SecureKeys
{
    /// <summary>Why a migration did not move the keys.</summary>
    internal enum KeyMigrationFailure
    {
        /// <summary>Nothing went wrong.</summary>
        None = 0,

        /// <summary>The new project was to be written over the project it is made from.</summary>
        SameFile,

        /// <summary>A file with the name of the new project exists already; a migration never overwrites a file.</summary>
        TargetExists,

        /// <summary>The project could not be read.</summary>
        SourceUnreadable,

        /// <summary>The key store refused to store a key; the detail is its error code.</summary>
        KeyStoreRefused,

        /// <summary>A key that was stored did not come back the way it went in.</summary>
        ReadBackDiffers,

        /// <summary>The new project could not be written.</summary>
        TargetNotWritten,

        /// <summary>The new project was written, but reading it again showed it is not what it has to be.</summary>
        VerificationFailed,

        /// <summary>The person cancelled.</summary>
        Cancelled,

        /// <summary>Something that was not expected went wrong; nothing was kept.</summary>
        Unexpected,
    }

    /// <summary>A slot of a task whose key was moved.</summary>
    /// <param name="Task">The number of the task in the project, from 1.</param>
    /// <param name="Slot">The slot id.</param>
    /// <param name="Reference">The reference the slot has now.</param>
    internal sealed record MovedKey(int Task, string Slot, string Reference);

    /// <summary>A slot of a task with a typed key that was not moved because it is not a key of the length its type has.</summary>
    /// <param name="Task">The number of the task in the project, from 1.</param>
    /// <param name="Slot">The slot id.</param>
    internal sealed record UnmovedKey(int Task, string Slot);

    /// <summary>
    /// What a migration did. It names tasks, slots, references and files; it holds no key and no part of one, so it may be
    /// shown, saved and sent.
    /// </summary>
    /// <param name="SourcePath">The project the keys were taken from.</param>
    /// <param name="TargetPath">The new project; empty when none was written.</param>
    /// <param name="Failure">Why nothing was moved, or <see cref="KeyMigrationFailure.None"/>.</param>
    /// <param name="FailureDetail">An error code that belongs to the failure; <c>0</c> otherwise.</param>
    /// <param name="Tasks">How many tasks the project has.</param>
    /// <param name="DifferentKeys">How many different keys were stored; slots with the same key share one.</param>
    /// <param name="Moved">The slots that were moved.</param>
    /// <param name="NotMoved">The slots with a typed key that was not moved.</param>
    internal sealed record KeyMigrationReport(
        string SourcePath,
        string TargetPath,
        KeyMigrationFailure Failure,
        int FailureDetail,
        int Tasks,
        int DifferentKeys,
        IReadOnlyList<MovedKey> Moved,
        IReadOnlyList<UnmovedKey> NotMoved)
    {
        /// <summary>Gets whether keys were moved, the new project was written and checked, and nothing failed.</summary>
        internal bool Succeeded => Failure == KeyMigrationFailure.None && Moved.Count > 0;

        /// <summary>Gets whether the project holds no typed key that could be moved; nothing was written then.</summary>
        internal bool NothingToMove => Failure == KeyMigrationFailure.None && Moved.Count == 0;
    }

    /// <summary>
    /// Moves the keys that are typed into the tasks of a project into the Windows Credential Manager and saves a new project
    /// that refers to them. The project it starts from is only read. Whatever it did is undone when a step fails: the keys
    /// it stored are removed and the new project is deleted.
    /// <para>
    /// The steps: read the project; find the typed keys; store each different key once under a random reference and read it
    /// back; bind the slots in memory; write the new project; read that again and check that no key is left in its text,
    /// that every slot is bound as intended and that every reference gives its key.
    /// </para>
    /// </summary>
    internal sealed class KeyMigration
    {
        private const int ReferenceAttempts = 5;

        private readonly IKeyMigrationProjects _projects;
        private readonly WindowsCredentialKeyProvider _keyStore;
        private readonly Func<string> _newReference;

        /// <summary>Creates the migration.</summary>
        /// <param name="projects">How projects are read and written.</param>
        /// <param name="keyStore">The Credential Manager provider; it stores and gives the keys.</param>
        /// <param name="newReference">Makes a reference that is not guessed; a random one unless a test says otherwise.</param>
        internal KeyMigration(IKeyMigrationProjects projects, WindowsCredentialKeyProvider keyStore, Func<string>? newReference = null)
        {
            _projects = projects ?? throw new ArgumentNullException(nameof(projects));
            _keyStore = keyStore ?? throw new ArgumentNullException(nameof(keyStore));
            _newReference = newReference ?? RandomReference;
        }

        /// <summary>Moves the typed keys of a project into the key store and writes a new project.</summary>
        /// <param name="sourcePath">The project. It is not changed.</param>
        /// <param name="targetPath">The new project. A file with that name must not exist.</param>
        /// <param name="cancellationToken">Cancelled when the person gives up; everything done so far is undone.</param>
        /// <returns>What was done, or why nothing was.</returns>
        internal async Task<KeyMigrationReport> MoveAsync(string sourcePath, string targetPath, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
            ArgumentException.ThrowIfNullOrWhiteSpace(targetPath);

            // A name that is no path at all: said as a failure, not thrown at the person.
            if (!TryFullPath(() => sourcePath, out var source))
            {
                return Refused(sourcePath, KeyMigrationFailure.SourceUnreadable);
            }

            if (!TryFullPath(() => _projects.TargetPathOf(targetPath), out var target))
            {
                return Refused(source, KeyMigrationFailure.TargetNotWritten);
            }

            var stored = new List<string>();
            var keys = new Dictionary<string, DistinctKey>(StringComparer.OrdinalIgnoreCase);
            string? written = null;
            var tasks = 0;
            var notMoved = new List<UnmovedKey>();

            KeyMigrationReport Fail(KeyMigrationFailure failure, int detail = 0)
            {
                Rollback(stored, written);

                return new KeyMigrationReport(source, string.Empty, failure, detail, tasks, 0, Array.Empty<MovedKey>(), notMoved);
            }

            if (string.Equals(source, target, StringComparison.OrdinalIgnoreCase))
            {
                return Fail(KeyMigrationFailure.SameFile);
            }

            if (File.Exists(target))
            {
                return Fail(KeyMigrationFailure.TargetExists);
            }

            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                var model = await _projects.ReadAsync(source, cancellationToken).ConfigureAwait(true);

                if (model == null)
                {
                    return Fail(KeyMigrationFailure.SourceUnreadable);
                }

                var plan = Plan(model, keys, notMoved, out tasks);

                if (plan.Count == 0)
                {
                    Log.ForContext<KeyMigration>().Information("A project holds no typed key to move ({NotMoved} typed values are no keys)", notMoved.Count);

                    return new KeyMigrationReport(source, string.Empty, KeyMigrationFailure.None, 0, tasks, 0, Array.Empty<MovedKey>(), notMoved);
                }

                foreach (var key in keys.Values)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    key.Reference = await FindUnusedReferenceAsync(key, cancellationToken).ConfigureAwait(true);

                    if (key.Reference == null)
                    {
                        return Fail(KeyMigrationFailure.KeyStoreRefused);
                    }

                    var error = _keyStore.Store(key.Reference, key.Bytes);

                    if (error != 0)
                    {
                        return Fail(KeyMigrationFailure.KeyStoreRefused, error);
                    }

                    stored.Add(key.Reference);

                    if (!await GivesBackAsync(key.Reference, "read-back", key.Bytes, cancellationToken).ConfigureAwait(true))
                    {
                        // A provider answers a token that was cancelled with a failure, not with an exception.
                        cancellationToken.ThrowIfCancellationRequested();

                        return Fail(KeyMigrationFailure.ReadBackDiffers);
                    }
                }

                foreach (var step in plan)
                {
                    step.Host.BindKeySlot(step.Slot, WindowsCredentialKeyProvider.ProviderId, step.Key.Reference!);
                }

                cancellationToken.ThrowIfCancellationRequested();

                // The new project carries the version of the application that writes it, not the version it was made with.
                model.ManifestVersion = new ChipTaskHandlerModel().ManifestVersion;

                try
                {
                    written = _projects.Write(model, target);
                }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is InvalidOperationException)
                {
                    Log.ForContext<KeyMigration>().Warning("The new project could not be written ({ExceptionType})", e.GetType().FullName);
                    written = null;
                }

                if (written == null)
                {
                    return Fail(KeyMigrationFailure.TargetNotWritten);
                }

                if (!await VerifyAsync(written, plan, keys.Values, cancellationToken).ConfigureAwait(true))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    return Fail(KeyMigrationFailure.VerificationFailed);
                }

                var moved = plan.Select(step => new MovedKey(step.Task, step.Slot, step.Key.Reference!)).ToList();
                Log.ForContext<KeyMigration>().Information("Moved {Slots} typed keys ({Different} different) of a project into the key store", moved.Count, keys.Count);

                return new KeyMigrationReport(source, written, KeyMigrationFailure.None, 0, tasks, keys.Count, moved, notMoved);
            }
            catch (OperationCanceledException)
            {
                return Fail(KeyMigrationFailure.Cancelled);
            }
            catch (Exception e)
            {
                // The type only: what a reader or a vault throws may contain a name or a value.
                Log.ForContext<KeyMigration>().Error("Moving the keys of a project failed unexpectedly ({ExceptionType})", e.GetType().FullName);

                return Fail(KeyMigrationFailure.Unexpected);
            }
            finally
            {
                foreach (var key in keys.Values)
                {
                    CryptographicOperations.ZeroMemory(key.Bytes);
                }
            }
        }

        /// <summary>
        /// Deletes the project that the keys were moved out of. It checks first, again, that the new project can be read and that
        /// every key it refers to is there, because the person may have changed the key store since. Nothing is deleted
        /// unless the migration succeeded and the new project and the original are different files.
        /// </summary>
        /// <param name="report">The report of a successful migration.</param>
        /// <param name="cancellationToken">Cancelled when the person gives up.</param>
        /// <returns><c>true</c> when the original was deleted.</returns>
        internal async Task<bool> DeleteOriginalAsync(KeyMigrationReport report, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(report);

            if (!report.Succeeded
                || string.Equals(report.SourcePath, report.TargetPath, StringComparison.OrdinalIgnoreCase)
                || !File.Exists(report.TargetPath)
                || !File.Exists(report.SourcePath))
            {
                return false;
            }

            try
            {
                if (!await StillGivesTheKeysAsync(report, cancellationToken).ConfigureAwait(true))
                {
                    return false;
                }

                File.Delete(report.SourcePath);

                return true;
            }
            catch (Exception e)
            {
                // Whatever goes wrong, the original stays: it is the one copy of the keys that is known to work.
                Log.ForContext<KeyMigration>().Warning("The original project was not deleted ({ExceptionType})", e.GetType().FullName);

                return false;
            }
        }

        private static bool TryFullPath(Func<string> path, out string fullPath)
        {
            try
            {
                fullPath = Path.GetFullPath(path());

                return true;
            }
            catch (Exception e) when (e is ArgumentException || e is NotSupportedException || e is IOException)
            {
                fullPath = string.Empty;

                return false;
            }
        }

        private static KeyMigrationReport Refused(string source, KeyMigrationFailure failure) =>
            new KeyMigrationReport(source, string.Empty, failure, 0, 0, 0, Array.Empty<MovedKey>(), Array.Empty<UnmovedKey>());

        private static string RandomReference() => "migrated/" + Convert.ToHexString(RandomNumberGenerator.GetBytes(8)).ToLowerInvariant();

        // The typed keys of every task, as slots, and the different keys among them. A value that is not a key of the length of
        // its slot cannot be stored, and stays where it is.
        private static List<Step> Plan(ChipTaskHandlerModel model, Dictionary<string, DistinctKey> keys, List<UnmovedKey> notMoved, out int tasks)
        {
            var plan = new List<Step>();
            tasks = 0;

            foreach (var task in model.TaskCollection)
            {
                tasks++;

                if (task is not IKeyMigrationHost host)
                {
                    continue;
                }

                foreach (var slot in host.EmbeddedKeySlots())
                {
                    var hex = host.GetEmbeddedKey(slot);

                    if (!IsKey(hex, host.ExpectedKeyHexLength(slot)))
                    {
                        notMoved.Add(new UnmovedKey(tasks, slot));
                        continue;
                    }

                    if (!keys.TryGetValue(hex, out var key))
                    {
                        key = new DistinctKey(hex);
                        keys.Add(hex, key);
                    }

                    plan.Add(new Step(tasks, host, slot, key));
                }
            }

            return plan;
        }

        private static bool IsKey(string text, int hexLength) =>
            text.Length == hexLength
            && hexLength % 2 == 0
            && hexLength / 2 <= WindowsCredentialKeyProvider.MaxKeyBytes
            && text.All(Uri.IsHexDigit);

        private async Task<string?> FindUnusedReferenceAsync(DistinctKey key, CancellationToken cancellationToken)
        {
            for (var attempt = 0; attempt < ReferenceAttempts; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var reference = _newReference();
                using var answer = await _keyStore.ResolveAsync(RequestFor(reference, "unused", key.Bytes.Length), cancellationToken).ConfigureAwait(true);

                if (!answer.IsResolved && answer.FailureReason == SecureKeyFailureReason.NotFound)
                {
                    return reference;
                }

                // A credential is there already, whatever it holds: take another reference. Only a vault that does not
                // answer ends the search.
                if (!answer.IsResolved && answer.FailureReason != SecureKeyFailureReason.InvalidKey && answer.FailureReason != SecureKeyFailureReason.NotAuthorized)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    return null;
                }
            }

            return null;
        }

        // Whether the reference gives the key that is expected: the key itself, or only its length when the key is not at hand.
        private async Task<bool> GivesBackAsync(string reference, string slot, byte[]? expected, CancellationToken cancellationToken, int length = 0)
        {
            using var answer = await _keyStore.ResolveAsync(RequestFor(reference, slot, expected?.Length ?? length), cancellationToken).ConfigureAwait(true);

            if (!answer.IsResolved || answer.Key is not SecureKeyLease lease)
            {
                return false;
            }

            return expected == null
                ? lease.Length == length
                : lease.Length == expected.Length && CryptographicOperations.FixedTimeEquals(lease.Key, expected);
        }

        // The new project is read again as another program would: no key is left in its text, every slot is bound as intended
        // and has no key typed into it, and every reference gives the key of the slot.
        private async Task<bool> VerifyAsync(string written, List<Step> plan, IEnumerable<DistinctKey> keys, CancellationToken cancellationToken)
        {
            var text = _projects.ReadProjectText(written);

            if (text == null || keys.Any(key => HoldsKey(text, key.Hex)))
            {
                return false;
            }

            var reloaded = await _projects.ReadAsync(written, cancellationToken).ConfigureAwait(true);

            if (reloaded == null)
            {
                return false;
            }

            foreach (var step in plan)
            {
                if (step.Task > reloaded.TaskCollection.Count || reloaded.TaskCollection[step.Task - 1] is not IKeyMigrationHost host)
                {
                    return false;
                }

                var binding = host.GetKeyBinding(step.Slot);

                if (binding == null
                    || binding.ProviderId != WindowsCredentialKeyProvider.ProviderId
                    || binding.Reference != step.Key.Reference
                    || host.HasEmbeddedKey(step.Slot))
                {
                    return false;
                }

                // The reference of the project that was read, not the one this run remembers.
                if (!await GivesBackAsync(binding.Reference, step.Slot, step.Key.Bytes, cancellationToken).ConfigureAwait(true))
                {
                    return false;
                }
            }

            return true;
        }

        // The key as it is written, and as bytes with spaces between them.
        private static bool HoldsKey(string text, string hex) =>
            text.Contains(hex, StringComparison.OrdinalIgnoreCase)
            || text.Contains(string.Join(" ", Enumerable.Range(0, hex.Length / 2).Select(i => hex.Substring(i * 2, 2))), StringComparison.OrdinalIgnoreCase);

        private async Task<bool> StillGivesTheKeysAsync(KeyMigrationReport report, CancellationToken cancellationToken)
        {
            var reloaded = await _projects.ReadAsync(report.TargetPath, cancellationToken).ConfigureAwait(true);

            if (reloaded == null)
            {
                return false;
            }

            foreach (var moved in report.Moved)
            {
                if (moved.Task > reloaded.TaskCollection.Count || reloaded.TaskCollection[moved.Task - 1] is not IKeyMigrationHost host)
                {
                    return false;
                }

                var binding = host.GetKeyBinding(moved.Slot);

                if (binding == null
                    || binding.Reference != moved.Reference
                    || !await GivesBackAsync(moved.Reference, moved.Slot, null, cancellationToken, host.ExpectedKeyHexLength(moved.Slot) / 2).ConfigureAwait(true))
                {
                    return false;
                }
            }

            return true;
        }

        private void Rollback(List<string> stored, string? written)
        {
            var notRemoved = 0;

            foreach (var reference in stored)
            {
                var error = _keyStore.Remove(reference);

                if (error != 0 && error != CredentialErrors.NotFound)
                {
                    notRemoved++;
                }
            }

            if (notRemoved > 0)
            {
                // A credential of this run stays in the vault; the references are not secret, but not worth a log line each.
                Log.ForContext<KeyMigration>().Warning("{Count} credentials that a failed migration stored could not be removed", notRemoved);
            }

            stored.Clear();

            if (written == null)
            {
                return;
            }

            try
            {
                File.Delete(written);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                Log.ForContext<KeyMigration>().Warning("The new project could not be deleted after a failure ({ExceptionType})", e.GetType().FullName);
            }
        }

        private static SecureKeyRequest RequestFor(string reference, string slot, int keyBytes) => new SecureKeyRequest
        {
            SlotId = slot,
            Reference = reference,
            Algorithm = keyBytes == 6 ? SecureKeyAlgorithm.Classic6Byte : keyBytes == 24 ? SecureKeyAlgorithm.TripleDes3K : SecureKeyAlgorithm.Aes128,
            Usage = SecureKeyUsage.Authenticate,
            Scope = SecureKeyScope.None,
            RunId = "migration",
            TaskIndex = "0",
            CardUid = null,
            Interactive = false,
        };

        /// <summary>A key that is moved: as bytes in memory that does not move, and the reference it gets.</summary>
        private sealed class DistinctKey
        {
            internal DistinctKey(string hex)
            {
                Hex = hex;
                Bytes = GC.AllocateArray<byte>(hex.Length / 2, pinned: true);
                Convert.FromHexString(hex, Bytes, out _, out _);
            }

            internal string Hex { get; }

            internal byte[] Bytes { get; }

            internal string? Reference { get; set; }
        }

        /// <summary>One slot of one task that is moved.</summary>
        private sealed record Step(int Task, IKeyMigrationHost Host, string Slot, DistinctKey Key);
    }
}
