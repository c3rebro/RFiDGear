using System;
using System.IO;
using Elatec.NET;

namespace RFiDGear.Infrastructure.ReaderProviders
{
    /// <summary>The call of the Ultralight operations that an exception came from; the same reader error means different things for them.</summary>
    internal enum UltralightCall
    {
        Select,
        Version,
        Read,
        Write,
        AuthUltralightC,
        AuthPassword
    }

    /// <summary>
    /// Turns what Elatec.NET throws into the normalized error codes. A refused write and a failed password both throw
    /// <c>ReaderException</c> with the reader error <c>AccessDenied</c>, so the call that was made decides which it was. The error of
    /// the exception is read from <c>ErrorNumber</c> when the library sets it, and else from the end of the message ("... error
    /// AccessDenied"): Elatec.NET 0.7.0-beta.1 never sets <c>ErrorNumber</c>.
    /// </summary>
    internal static class ElatecUltralightErrors
    {
        private const string ErrorMarker = "error ";

        /// <summary>
        /// The normalized error of an exception.
        /// </summary>
        /// <param name="exception">What was thrown.</param>
        /// <param name="call">The call that threw it.</param>
        /// <returns>The error code.</returns>
        public static ERROR Classify(Exception exception, UltralightCall call)
        {
            switch (exception)
            {
                case ReaderException _:
                    return TryGetReaderError(exception, out var error) ? Classify(error, call) : ERROR.Unknown;

                case TwnException _:
                case IOException _:
                case TimeoutException _:
                case UnauthorizedAccessException _:
                case ObjectDisposedException _:
                    return ERROR.TransportError;

                default:
                    return ERROR.Unknown;
            }
        }

        /// <summary>
        /// The reader error of an exception.
        /// </summary>
        /// <param name="exception">What was thrown.</param>
        /// <param name="error">The reader error, when it is one.</param>
        /// <returns><c>false</c> for any other exception, and for a reader exception that names no error.</returns>
        public static bool TryGetReaderError(Exception exception, out ReaderError error)
        {
            error = ReaderError.None;

            if (!(exception is ReaderException reader))
            {
                return false;
            }

            if (reader.ErrorNumber != ReaderError.None)
            {
                error = reader.ErrorNumber;

                return true;
            }

            // "Call was not successfull, error AccessDenied": the name after the last "error ".
            var message = reader.Message;
            var at = message == null ? -1 : message.LastIndexOf(ErrorMarker, StringComparison.Ordinal);

            if (at < 0)
            {
                return false;
            }

            var name = message.Substring(at + ErrorMarker.Length).Trim();

            // A name and not a number, which Enum.TryParse would take as a value.
            if (name.Length == 0 || !char.IsLetter(name[0]) || !Enum.TryParse(name, false, out ReaderError parsed) || !Enum.IsDefined(typeof(ReaderError), parsed) || parsed == ReaderError.None)
            {
                return false;
            }

            error = parsed;

            return true;
        }

        /// <summary>
        /// A description of an exception for the log: the name of its type and, for a reader exception, the name of the reader error.
        /// Never the message, which can carry what the caller passed in.
        /// </summary>
        /// <param name="exception">What was thrown.</param>
        /// <returns>The description.</returns>
        public static string Describe(Exception exception)
        {
            var type = exception.GetType().Name;

            return TryGetReaderError(exception, out var error) ? type + " " + error : type;
        }

        private static ERROR Classify(ReaderError error, UltralightCall call)
        {
            switch (error)
            {
                case ReaderError.AccessDenied:
                    // The card said no. To a password or a key that is a failed authentication, to anything else a refusal.
                    return call == UltralightCall.AuthPassword || call == UltralightCall.AuthUltralightC ? ERROR.AuthFailure : ERROR.PermissionDenied;

                case ReaderError.OutOfMemory:
                case ReaderError.IsAlreadyInit:
                case ReaderError.NotInit:
                case ReaderError.IsAlreadyOpen:
                case ReaderError.NotOpen:
                case ReaderError.Timeout:
                case ReaderError.PecErr:
                case ReaderError.Ovr:
                case ReaderError.AF:
                case ReaderError.Arlo:
                case ReaderError.BErr:
                    return ERROR.TransportError;

                case ReaderError.NotSupported:
                case ReaderError.Range:
                case ReaderError.Parameter:
                case ReaderError.Compatibility:
                    return ERROR.ProtocolConstraint;

                default:
                    return ERROR.Unknown;
            }
        }
    }
}
