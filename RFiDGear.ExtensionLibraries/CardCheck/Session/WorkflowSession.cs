using System;
using System.Linq;

namespace RFiDGear.ExtensionLibraries.CardCheck
{
    /// <summary>The in-memory implementation of <see cref="IWorkflowSession"/>.</summary>
    internal sealed class WorkflowSession : IWorkflowSession
    {
        private readonly ICardCheckLog _log;
        private readonly object _gate = new object();
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
        // lock is released, so a handler may read the session or change it again.
        private Result Change(Func<SessionSnapshot, Result<SessionSnapshot>> build)
        {
            SessionSnapshot next;

            lock (_gate)
            {
                var result = build(_current);

                if (!result.IsSuccess)
                {
                    return Result.Fail(result.Failure!);
                }

                next = result.Value;
                _current = next;
            }

            Raise(next);

            return Result.Ok();
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
