#nullable enable
using System;
using System.Threading;

using RFiDGear.Bridge;

namespace RFiDGear.Services.ExternalControl
{
    /// <summary>
    /// Where a run came from, as far as RFiDGear can tell: who started it, and for a run of a client over the bridge which
    /// request it answers and which revision of the project it started with. It holds no key, no UID and no path.
    /// </summary>
    /// <param name="Source">Who started the run, one of <see cref="RunSources"/>.</param>
    /// <param name="RequestId">The request id of the client, for a run of a client over the bridge; otherwise <see langword="null"/>.</param>
    /// <param name="ProjectRevision">The revision the run started with, for a run of a client over the bridge when the host could tell it.</param>
    internal sealed record RunOrigin(string Source, string? RequestId = null, string? ProjectRevision = null)
    {
        /// <summary>Gets what is said about a run that RFiDGear cannot place: an extension, a button of a task dialog, a run that is over.</summary>
        internal static RunOrigin Unknown { get; } = new RunOrigin(RunSources.Other);
    }

    /// <summary>
    /// Remembers which run the main window is running right now and who asked for it, because only the window knows: a run of
    /// the window and a run of automatic mode are the same code, and an extension has neither. One run holds the execution
    /// slot at a time, so one mark is enough. It holds a run id and a source, nothing else.
    /// </summary>
    internal sealed class WindowRunMarker
    {
        private sealed record Mark(string RunId, string Source);

        private Mark? _current;

        /// <summary>Says that the window has begun a run.</summary>
        /// <param name="runId">The id of the run.</param>
        /// <param name="source">Who asked for it: <see cref="RunSources.Gui"/> or <see cref="RunSources.AutoMode"/>.</param>
        internal void Begin(string runId, string source) => Volatile.Write(ref _current, new Mark(runId, source));

        /// <summary>Says that the run has ended. A mark that is another run's by now is left alone.</summary>
        /// <param name="runId">The id of the run that ended.</param>
        internal void End(string runId)
        {
            var seen = Volatile.Read(ref _current);

            if (seen != null && string.Equals(seen.RunId, runId, StringComparison.Ordinal))
            {
                Interlocked.CompareExchange(ref _current, null, seen);
            }
        }

        /// <summary>Says who asked for a run of the window, if this is the one that is going on.</summary>
        /// <param name="runId">The id of a run, or <see langword="null"/>.</param>
        /// <returns>The source, or <see langword="null"/> when the window is not running this run.</returns>
        internal string? SourceOf(string? runId)
        {
            var mark = Volatile.Read(ref _current);

            return mark != null && runId != null && string.Equals(mark.RunId, runId, StringComparison.Ordinal) ? mark.Source : null;
        }
    }

    /// <summary>
    /// Says where a run comes from, for the request of a key: a run of a client of the bridge is in the table of the bridge, a run
    /// of the window or of automatic mode is the one the window marked, and everything else is unknown. A run that the bridge
    /// started wins over a mark of the window, so that a stale mark can never make a client's run look like another.
    /// </summary>
    internal sealed class RunOriginResolver
    {
        private readonly RunRegistry? _registry;
        private readonly Func<string?, string?>? _windowRunSource;

        /// <summary>Creates the resolver.</summary>
        /// <param name="registry">The runs of the clients of the bridge, or <see langword="null"/> when the host cannot run for a client.</param>
        /// <param name="windowRunSource">Says who asked for the run of the window with this id, or <see langword="null"/> (see <see cref="WindowRunMarker.SourceOf"/>).</param>
        internal RunOriginResolver(RunRegistry? registry, Func<string?, string?>? windowRunSource)
        {
            _registry = registry;
            _windowRunSource = windowRunSource;
        }

        /// <summary>Finds where a run came from.</summary>
        /// <param name="runId">The id of the run the task belongs to; <see langword="null"/> for a command that no run started.</param>
        /// <returns>The origin; <see cref="RunOrigin.Unknown"/> when RFiDGear cannot place the run.</returns>
        internal RunOrigin Resolve(string? runId)
        {
            if (string.IsNullOrEmpty(runId))
            {
                return RunOrigin.Unknown;
            }

            // Admitting counts: the host starts the first task before the record says that the run is in flight.
            var record = _registry?.Find(null, runId);

            if (record != null && (record.State == RunRecordState.Admitting || record.State == RunRecordState.Running))
            {
                return new RunOrigin(RunSources.Bridge, record.RequestId, record.ProjectRevision);
            }

            var source = _windowRunSource?.Invoke(runId);

            return source == null ? RunOrigin.Unknown : new RunOrigin(source);
        }
    }
}
