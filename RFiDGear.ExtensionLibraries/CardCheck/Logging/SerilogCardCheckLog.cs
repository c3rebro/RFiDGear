using System;
using Serilog;

namespace RFiDGear.ExtensionLibraries.CardCheck
{
    /// <summary>
    /// Writes to the host's Serilog pipeline. This is the only class in the library that touches Serilog: the
    /// host owns the one loaded copy, and everything the library logs ends up in the same file as the host's own
    /// lines. The logger is read from <see cref="Log.Logger"/> on every call, so a pipeline that the host replaces
    /// later (or a test that installs its own) is followed without the library having to be told.
    /// </summary>
    internal sealed class SerilogCardCheckLog : ICardCheckLog
    {
        private const string SourceContext = "CardCheck";

        private readonly Func<ILogger> _loggerSource;

        /// <summary>Creates a log that follows the host's current <see cref="Log.Logger"/>.</summary>
        public SerilogCardCheckLog()
            : this(() => Log.Logger)
        {
        }

        /// <summary>Creates a log that writes to the logger the given function returns at the time of each call.</summary>
        /// <param name="loggerSource">Supplies the logger; called once per message.</param>
        internal SerilogCardCheckLog(Func<ILogger> loggerSource)
        {
            _loggerSource = loggerSource ?? throw new ArgumentNullException(nameof(loggerSource));
        }

        /// <inheritdoc />
        public void Information(string messageTemplate, params object?[] values) =>
            Current().Information(messageTemplate, values!);

        /// <inheritdoc />
        public void Warning(string messageTemplate, params object?[] values) =>
            Current().Warning(messageTemplate, values!);

        /// <inheritdoc />
        public void Error(string messageTemplate, params object?[] values) =>
            Current().Error(messageTemplate, values!);

        /// <inheritdoc />
        public void Unexpected(string operation, UnexpectedFault fault)
        {
            if (fault == null)
            {
                throw new ArgumentNullException(nameof(fault));
            }

            Current().Error(
                "Unexpected failure in {Operation}: {FaultType} (HResult {FaultHResult}){NewLine}{FaultStackTrace}",
                operation,
                fault.TypeName,
                fault.HResult,
                Environment.NewLine,
                fault.StackTrace);
        }

        private ILogger Current() => _loggerSource().ForContext(Serilog.Core.Constants.SourceContextPropertyName, SourceContext);
    }
}
