using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace RFiDGear.Contracts
{
    /// <summary>
    /// Immutable runtime parameters supplied to <see cref="ITaskExecHost.ExecuteAllAsync(ExecutionRequest, System.Threading.CancellationToken)"/>.
    /// Variables are matched case-insensitively.
    /// <para>
    /// Placeholder variables: keys containing <c>$</c> (for example <c>$JOBNUMBER</c> and <c>$CHIPNUMBER</c>)
    /// are substituted into task text and report fields that contain the same token.
    /// </para>
    /// <para>
    /// Report variables, interpreted exactly as the equivalent startup arguments are:
    /// <list type="bullet">
    /// <item><description>
    /// <c>REPORTTARGETPATH</c> (alias <c>REPORTOUTPUTPATH</c>): fully qualified path of the report <em>file</em> to
    /// create, not a folder. Its folder must already exist. A <c>?</c>, <c>??</c>, or <c>???</c> in the path is
    /// replaced by the next free number in that folder. A project that contains a "create report" task needs this variable;
    /// without it the report task cannot run unattended and the run ends with <see cref="ExecutionFailureReason.Faulted"/>.
    /// </description></item>
    /// <item><description>
    /// <c>REPORTTEMPLATEFILE</c> (alias <c>REPORTTEMPLATEPATH</c>): path of an existing PDF template to use for the report.
    /// </description></item>
    /// </list>
    /// A report variable that is supplied but cannot be honoured (missing directory or file) makes
    /// <see cref="ITaskExecHost.ExecuteAllAsync(ExecutionRequest, System.Threading.CancellationToken)"/> throw
    /// <see cref="ArgumentException"/> before the run is accepted, so no card operation happens only to fail at the report step.
    /// A blank value is treated as "not supplied".
    /// </para>
    /// Do not include secret key material in variables; values may appear in structured logs.
    /// </summary>
    public sealed class ExecutionRequest
    {
        /// <summary>Shared empty request with no runtime variables.</summary>
        public static readonly ExecutionRequest Empty = new ExecutionRequest();

        /// <summary>
        /// Runtime variable dictionary. Keys are matched case-insensitively by the execution service.
        /// </summary>
        public IReadOnlyDictionary<string, string> Variables { get; }

        /// <summary>Creates a request with no runtime variables.</summary>
        public ExecutionRequest()
        {
            Variables = new ReadOnlyDictionary<string, string>(
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
        }

        /// <summary>Creates a request from an existing variable collection.</summary>
        /// <param name="variables">Variable pairs; keys are normalized to case-insensitive matching.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="variables"/> is null.</exception>
        public ExecutionRequest(IEnumerable<KeyValuePair<string, string>> variables)
        {
            if (variables == null) throw new ArgumentNullException(nameof(variables));
            var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in variables)
                d[kv.Key] = kv.Value;
            Variables = new ReadOnlyDictionary<string, string>(d);
        }
    }
}
