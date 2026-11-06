#nullable enable
using RFiDGear.Services.TaskExecution;

using System;

namespace RFiDGear.Infrastructure.ExtensionHost
{
    /// <summary>
    /// A description of an exception that is safe to write to the log.
    /// <para>
    /// Exception messages can carry key material or payload text (for example <c>"Key=..."</c>). The task
    /// execution service redacts them with <see cref="TaskExecutionLogSanitizer"/> before logging, so the host
    /// must not attach the raw exception to a log event, which would write the original text a second time.
    /// This keeps only the type name, the sanitized (and length-limited) message, and the sanitized stack
    /// trace. Inner exceptions are deliberately left out.
    /// </para>
    /// </summary>
    internal sealed class SanitizedException
    {
        /// <summary>Creates the description for <paramref name="exception"/>.</summary>
        /// <param name="exception">The exception to describe.</param>
        public SanitizedException(Exception exception)
        {
            if (exception == null)
            {
                throw new ArgumentNullException(nameof(exception));
            }

            TypeName = exception.GetType().Name;
            Message = TaskExecutionLogSanitizer.Sanitize(exception.Message) ?? string.Empty;
            StackTrace = TaskExecutionLogSanitizer.Sanitize(exception.StackTrace) ?? string.Empty;
        }

        /// <summary>Short type name of the exception, such as <c>InvalidOperationException</c>.</summary>
        public string TypeName { get; }

        /// <summary>The exception message with key-like content redacted.</summary>
        public string Message { get; }

        /// <summary>The start of the stack trace with key-like content redacted.</summary>
        public string StackTrace { get; }
    }
}
