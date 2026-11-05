using System;
using System.Collections.Generic;

namespace RFiDGear.Contracts
{
    /// <summary>
    /// Immutable runtime parameters supplied to <see cref="ITaskExecHost.ExecuteAllAsync(ExecutionRequest, System.Threading.CancellationToken)"/>.
    /// Variables are matched case-insensitively. Supported keys include
    /// <c>REPORTTARGETPATH</c>, <c>REPORTTEMPLATEFILE</c>, <c>$JOBNUMBER</c>, and <c>$CHIPNUMBER</c>.
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
            Variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
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
            Variables = d;
        }
    }
}
