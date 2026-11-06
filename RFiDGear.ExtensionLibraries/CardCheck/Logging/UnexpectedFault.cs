using System;

namespace RFiDGear.ExtensionLibraries.CardCheck
{
    /// <summary>
    /// What may be logged about an exception nobody expected. It deliberately leaves out the message and the
    /// <see cref="Exception.Data"/> dictionary: driver and file-system messages routinely contain server names,
    /// user names, paths and connection details, and a stack trace is enough to find the place.
    /// </summary>
    public sealed record UnexpectedFault(string TypeName, int HResult, string StackTrace)
    {
        /// <summary>Takes the loggable part of an exception.</summary>
        /// <param name="exception">The exception to describe.</param>
        /// <returns>The type name, the HResult and the stack trace; never the message or the data.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="exception"/> is null.</exception>
        public static UnexpectedFault From(Exception exception)
        {
            if (exception == null)
            {
                throw new ArgumentNullException(nameof(exception));
            }

            return new UnexpectedFault(
                exception.GetType().FullName ?? exception.GetType().Name,
                exception.HResult,
                exception.StackTrace ?? string.Empty);
        }
    }
}
