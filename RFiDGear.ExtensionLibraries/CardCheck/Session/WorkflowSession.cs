using System;
using System.Collections.Generic;
using System.Linq;

namespace RFiDGear.ExtensionLibraries.CardCheck
{
    /// <summary>The in-memory implementation of <see cref="IWorkflowSession"/>.</summary>
    internal sealed class WorkflowSession : IWorkflowSession
    {
        private readonly ICardCheckLog _log;
        private readonly object _gate = new object();

        // Snapshots that were swapped in and whose event has not been raised yet, oldest first, and whether a caller
        // is raising them. Together they keep the events in the order of the versions.
        private readonly Queue<SessionSnapshot> _pending = new Queue<SessionSnapshot>();
        private bool _delivering;
        private SessionSnapshot _current = SessionSnapshot.Empty;

        /// <summary>Creates an empty session.</summary>
        /// <param name="log">Where a fault in a change handler is noted.</param>
        public WorkflowSession(ICardCheckLog log)
        {
            _log = log ?? throw new ArgumentNullException(nameof(log));
        }

        /// <inheritdoc />
        public event EventHandler<SessionSnapshot>? Changed;

        /// <inheritdoc />
        public SessionSnapshot Current
        {
            get
            {
                lock (_gate)
                {
                    return _current;
                }
            }
        }

        /// <inheritdoc />
        public Result Begin(WorkflowOrder order, ReportLanguage language)
        {
            if (order == null)
            {
                throw new ArgumentNullException(nameof(order));
            }

            if (string.IsNullOrWhiteSpace(order.Id))
            {
                return Result.Fail(new Failure(FailureKind.InvalidInput, "session.order.invalid", "The order has no identifier."));
            }

            if (!Enum.IsDefined(typeof(ReportLanguage), language))
            {
                return Result.Fail(new Failure(FailureKind.InvalidInput, "session.language.invalid", "The report language is not known."));
            }

            return Change(snapshot => Result<SessionSnapshot>.Ok(new SessionSnapshot { Order = order, Language = language, Version = snapshot.Version + 1 }));
        }

        /// <inheritdoc />
        public Result RecordFindings(ReportAnalysis analysis)
        {
            if (analysis == null)
            {
                throw new ArgumentNullException(nameof(analysis));
            }

            return Change(snapshot =>
            {
                if (snapshot.Order == null)
                {
                    return NoOrder();
                }

                var selection = TemplateRules.Select(analysis);

                return Result<SessionSnapshot>.Ok(snapshot with
                {
                    Findings = analysis,
                    Selection = selection,
                    ChosenTemplate = selection.Default,
                    Version = snapshot.Version + 1,
                });
            });
        }

        /// <inheritdoc />
        public Result ChooseTemplate(LsmTemplate template)
        {
            if (template == null)
            {
                throw new ArgumentNullException(nameof(template));
            }

            return Change(snapshot =>
            {
                if (snapshot.Order == null)
                {
                    return NoOrder();
                }

                var match = snapshot.Selection?.Available.FirstOrDefault(t => t.Name == template.Name);

                return match == null
                    ? Result<SessionSnapshot>.Fail(new Failure(FailureKind.InvalidInput, "session.template.notAvailable", "That template does not fit the chip."))
                    : Result<SessionSnapshot>.Ok(snapshot with { ChosenTemplate = match, Version = snapshot.Version + 1 });
            });
        }

        /// <inheritdoc />
        public void Reset()
        {
            Change(snapshot => Result<SessionSnapshot>.Ok(new SessionSnapshot { Version = snapshot.Version + 1 }));
        }

        private static Result<SessionSnapshot> NoOrder() =>
            Result<SessionSnapshot>.Fail(new Failure(FailureKind.InvalidInput, "session.noOrder", "No check has begun."));

        // The new snapshot is built from the current one and swapped in under the lock; the event is raised after the
        // lock is released, so a handler may read the session or change it again. Events are raised one at a time, in
        // the order of the versions: if a handler (or another thread) changes the session while events are being
        // raised, its snapshot is queued and raised by the caller that is already delivering, after the ones before it.
        private Result Change(Func<SessionSnapshot, Result<SessionSnapshot>> build)
        {
            lock (_gate)
            {
                var result = build(_current);

                if (!result.IsSuccess)
                {
                    return Result.Fail(result.Failure!);
                }

                _current = result.Value;
                _pending.Enqueue(result.Value);

                if (_delivering)
                {
                    return Result.Ok();
                }

                _delivering = true;
            }

            DeliverPending();

            return Result.Ok();
        }

        private void DeliverPending()
        {
            var drained = false;

            try
            {
                while (true)
                {
                    SessionSnapshot next;

                    lock (_gate)
                    {
                        if (_pending.Count == 0)
                        {
                            _delivering = false;
                            drained = true;

                            return;
                        }

                        next = _pending.Dequeue();
                    }

                    Raise(next);
                }
            }
            finally
            {
                if (!drained)
                {
                    // Raise catches what a handler throws; this is for anything worse, so a later change can deliver.
                    lock (_gate)
                    {
                        _delivering = false;
                    }
                }
            }
        }

        private void Raise(SessionSnapshot snapshot)
        {
            var handlers = Changed;

            if (handlers == null)
            {
                return;
            }

            foreach (var handler in handlers.GetInvocationList().Cast<EventHandler<SessionSnapshot>>())
            {
                try
                {
                    handler(this, snapshot);
                }
                catch (Exception ex)
                {
                    _log.Unexpected("session.changed", UnexpectedFault.From(ex));
                }
            }
        }
    }
}
