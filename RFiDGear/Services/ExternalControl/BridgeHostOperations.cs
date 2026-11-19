#nullable enable
using System;
using System.IO;
using System.Threading.Tasks;

using RFiDGear.Bridge;
using RFiDGear.Contracts;

namespace RFiDGear.Services.ExternalControl
{
    /// <summary>
    /// What <c>loadProject</c> and <c>scanReader</c> do on the host. A request is checked here before the host sees it (the
    /// answer names the field, never the value), a refusal of the host becomes an error of the bridge with its own code, and
    /// no path, name or text of the host ever goes into an error. The host decides what is busy: this does not look at its own
    /// bookkeeping first, because the answer of the host is the truth at that moment.
    /// </summary>
    internal sealed class BridgeHostOperations
    {
        private readonly IRFiDGearExtensionHost _host;
        private readonly Func<string?> _busyActivity;
        private readonly Func<Action, Task> _dispatchToUi;
        private readonly TimeSpan _uiTimeout;

        /// <summary>Creates the operations.</summary>
        /// <param name="host">The host service of RFiDGear.</param>
        /// <param name="busyActivity">
        /// Says what the application is busy with at the moment (one of <see cref="BusyActivities"/>), or <see langword="null"/>
        /// when it cannot tell; asked only after the host has refused a load as busy, as a hint for the client.
        /// </param>
        /// <param name="dispatchToUi">
        /// Runs an action on the thread of the main window, which the revision of a project is read on; <see langword="null"/> runs
        /// it on the calling thread (a host that has no window).
        /// </param>
        /// <param name="uiTimeout">How long to wait for the main window to read the revision; the answer has none after that.</param>
        internal BridgeHostOperations(IRFiDGearExtensionHost host, Func<string?>? busyActivity = null, Func<Action, Task>? dispatchToUi = null, TimeSpan? uiTimeout = null)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));
            _busyActivity = busyActivity ?? (() => null);
            _dispatchToUi = dispatchToUi ?? (action =>
            {
                action();

                return Task.CompletedTask;
            });
            _uiTimeout = uiTimeout ?? TimeSpan.FromSeconds(2);
        }

        /// <summary>Loads a project into the main window, replacing the one that is there.</summary>
        /// <param name="request">The request.</param>
        /// <returns>The path as the host knows it and the number of tasks.</returns>
        internal async Task<LoadProjectResponse> LoadProjectAsync(LoadProjectRequest request)
        {
            var path = ValidatePath(request);

            // Replacing the project under a machine that is working through cards would hand it a different job half way.
            // This is the policy; the exclusion itself is the coordinator's: a load holds its slot, automatic mode cannot be
            // switched on while the slot is held (TryStart answers busy), and none of its runs can start. So a mode that is
            // switched on between this check and the admission of the load starts with the new project and has processed
            // no card with a half-replaced one; the window is too narrow to be worth a lock that every other caller of
            // LoadAsync (the extensions) would have to share.
            if (_host.AutoMode.IsAutoModeEnabled)
            {
                throw BridgeFault.AutoModeActive();
            }

            var result = await _host.Project.LoadAsync(path).ConfigureAwait(false);

            if (!result.Success)
            {
                throw Refusal(result.Error);
            }

            return new LoadProjectResponse { Path = result.CanonicalPath ?? path, TaskCount = result.TaskCount, Revision = await ReadRevisionAsync().ConfigureAwait(false) };
        }

        // The revision of the project that was just loaded. The load is done and the answer is valid without it, so a window that
        // does not answer in time, or a project that cannot be fingerprinted, gives no revision and no error.
        private async Task<string?> ReadRevisionAsync()
        {
            if (_host.Project is not Infrastructure.ExtensionHost.IProjectRevisionSource source)
            {
                return null;
            }

            string? revision = null;

            try
            {
                await _dispatchToUi(() => revision = source.GetProjectRevision()).WaitAsync(_uiTimeout).ConfigureAwait(false);
            }
            catch (Exception e) when (e is TimeoutException || e is OperationCanceledException || e is InvalidOperationException)
            {
                return null;
            }

            return revision;
        }

        /// <summary>Looks at the reader once.</summary>
        /// <returns>What the reader sees; a reader that is busy or has no card is an answer.</returns>
        internal async Task<ScanReaderResponse> ScanReaderAsync()
        {
            var snapshot = await _host.Reader.ScanAsync().ConfigureAwait(false);

            return new ScanReaderResponse { Reader = BridgeMapper.ToDto(snapshot) };
        }

        // A path the host is asked to open must be a full path to a file, never relative to a directory only RFiDGear knows, and
        // never a device. The path that is passed on is the normalised one (no "." or ".." segments).
        private static string ValidatePath(LoadProjectRequest? request)
        {
            if (request == null)
            {
                throw BridgeFault.InvalidRequest("request");
            }

            var path = request.Path;

            if (string.IsNullOrWhiteSpace(path) || path.Length > BridgeProtocol.MaxPathLength || path.Contains('\0') || IsDevicePath(path) || !Path.IsPathFullyQualified(path))
            {
                throw BridgeFault.InvalidRequest("path");
            }

            try
            {
                return Path.GetFullPath(path);
            }
            catch (Exception e) when (e is ArgumentException || e is NotSupportedException || e is PathTooLongException || e is System.Security.SecurityException)
            {
                throw BridgeFault.InvalidRequest("path");
            }
        }

        // Windows takes "/" for "\" and treats the first two characters the same either way, so the separators are made equal
        // before the prefix is looked at: "\\./pipe/x" is a device path as much as "\\.\pipe\x". Three separators in a row at the
        // start (an empty server name) are no path of a project either, and are refused rather than guessed at.
        private static bool IsDevicePath(string path)
        {
            var normalised = path.Replace('/', '\\');

            return normalised.StartsWith(@"\\?\", StringComparison.Ordinal)
                || normalised.StartsWith(@"\\.\", StringComparison.Ordinal)
                || normalised.StartsWith(@"\??\", StringComparison.Ordinal)
                || normalised.StartsWith(@"\\\", StringComparison.Ordinal);
        }

        private BridgeFault Refusal(ProjectOpenError error) => error switch
        {
            ProjectOpenError.PathNotFound => BridgeFault.ProjectNotFound(),
            ProjectOpenError.UnsupportedFormat => BridgeFault.ProjectUnsupportedFormat(),
            ProjectOpenError.Busy => BridgeFault.Busy(_busyActivity() ?? BusyActivities.Other),
            _ => BridgeFault.ProjectLoadFailed(),
        };
    }
}
