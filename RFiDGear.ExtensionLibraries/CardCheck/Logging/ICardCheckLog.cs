namespace RFiDGear.ExtensionLibraries.CardCheck
{
    /// <summary>
    /// Where the library writes what happened. Services take this instead of calling a logging framework, so what
    /// reaches the log file is decided in one place and tests can read it.
    /// <para>
    /// Callers must never put a server name, user name, connection string, password, table name or any report or
    /// card content into a message or an argument. Exceptions are not accepted at all: a boundary turns one into
    /// a <see cref="Failure"/> and, for an unexpected one, logs only an <see cref="UnexpectedFault"/>.
    /// </para>
    /// </summary>
    public interface ICardCheckLog
    {
        /// <summary>Writes something that happened as expected and is worth a line.</summary>
        /// <param name="messageTemplate">A message template with named holes, for example <c>Report {Step} staged</c>.</param>
        /// <param name="values">The values for the holes, in order.</param>
        void Information(string messageTemplate, params object?[] values);

        /// <summary>Writes something that went wrong in a way the operator can recover from.</summary>
        /// <param name="messageTemplate">A message template with named holes.</param>
        /// <param name="values">The values for the holes, in order.</param>
        void Warning(string messageTemplate, params object?[] values);

        /// <summary>Writes a failure of an operation.</summary>
        /// <param name="messageTemplate">A message template with named holes.</param>
        /// <param name="values">The values for the holes, in order.</param>
        void Error(string messageTemplate, params object?[] values);

        /// <summary>Writes an exception nobody expected, with the details that are safe to keep.</summary>
        /// <param name="operation">A fixed word for what was running, for example <c>order.finalize</c>.</param>
        /// <param name="fault">What is known about the exception; see <see cref="UnexpectedFault.From"/>.</param>
        void Unexpected(string operation, UnexpectedFault fault);
    }
}
