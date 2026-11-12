#nullable enable
using System;
using System.Threading;

namespace RFiDGear.Services.SecureKeys
{
    /// <summary>
    /// What a command needs to ask a provider for a key: the registry, and the circumstances the providers are told about.
    /// It holds no key. It reaches a command through <see cref="KeyResolutionScope"/>, not through a property of the task:
    /// the task is saved as the project, and anything stored in it would be saved with it.
    /// </summary>
    internal sealed class KeyResolutionContext
    {
        /// <summary>Creates a context.</summary>
        /// <param name="registry">The providers.</param>
        /// <param name="runId">The execution run, or <c>null</c> for a command that no run started.</param>
        /// <param name="taskIndex">The task index as stored in the project; empty outside a task.</param>
        /// <param name="cardUid">The UID of the card the run read at its start, or <c>null</c>.</param>
        /// <param name="interactive">Whether a provider may ask a person.</param>
        /// <param name="resolutionStarted">Called before the first provider is asked; may be <c>null</c>.</param>
        /// <param name="resolutionFinished">Called when the last provider has answered; may be <c>null</c>.</param>
        /// <param name="cancellationToken">
        /// The cancellation of the run. A cancelled run stops asking, and a command whose run was cancelled while a provider
        /// answered goes on to no card call.
        /// </param>
        internal KeyResolutionContext(
            SecureKeyRegistry registry,
            string? runId,
            string taskIndex,
            string? cardUid,
            bool interactive,
            Action? resolutionStarted = null,
            Action? resolutionFinished = null,
            CancellationToken cancellationToken = default)
        {
            Registry = registry ?? throw new ArgumentNullException(nameof(registry));
            RunId = runId;
            TaskIndex = taskIndex ?? string.Empty;
            CardUid = cardUid;
            Interactive = interactive;
            ResolutionStarted = resolutionStarted;
            ResolutionFinished = resolutionFinished;
            CancellationToken = cancellationToken;
        }

        /// <summary>Gets the cancellation of the run; <c>default</c> (never cancelled) for a command that no run started.</summary>
        internal CancellationToken CancellationToken { get; }

        /// <summary>Gets the providers.</summary>
        internal SecureKeyRegistry Registry { get; }

        /// <summary>Gets the execution run, or <c>null</c>.</summary>
        internal string? RunId { get; }

        /// <summary>Gets the task index as stored in the project.</summary>
        internal string TaskIndex { get; }

        /// <summary>Gets the UID of the card the run read at its start, or <c>null</c>.</summary>
        internal string? CardUid { get; }

        /// <summary>Gets whether a provider may ask a person.</summary>
        internal bool Interactive { get; }

        /// <summary>
        /// Gets what runs before the first provider is asked. The execution service stops its watchdog here: a provider may
        /// take longer than the watchdog allows for a whole task, and the registry has a time limit of its own.
        /// </summary>
        internal Action? ResolutionStarted { get; }

        /// <summary>Gets what runs when the last provider has answered, whatever the answers were.</summary>
        internal Action? ResolutionFinished { get; }
    }
}
