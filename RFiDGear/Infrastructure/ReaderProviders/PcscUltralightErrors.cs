using System;
using System.IO;
using LibLogicalAccess;

namespace RFiDGear.Infrastructure.ReaderProviders
{
    /// <summary>
    /// What a PC/SC card said no with, as a managed exception: the page operations of the PC/SC provider catch the native
    /// <c>CardException</c> where they call LibLogicalAccess and throw this instead, so the code that classifies them has no native
    /// object to deal with (and the tests no native object to build). It carries the error code and nothing of the native message,
    /// which can hold what was sent.
    /// </summary>
    internal sealed class PcscCardRefusal : Exception
    {
        /// <summary>Creates the refusal.</summary>
        /// <param name="code">The error code of the card exception.</param>
        public PcscCardRefusal(CardException.ErrorType code)
            : base("The card refused the call.")
        {
            Code = code;
        }

        /// <summary>Gets the error code of the card exception.</summary>
        public CardException.ErrorType Code { get; }
    }

    /// <summary>
    /// Turns what LibLogicalAccess throws into the normalized error codes. A card that says no says it with an error code, and the
    /// same code means a failed authentication to an authentication and a refusal to a page operation: the call decides, as it
    /// does for the Elatec reader.
    /// </summary>
    internal static class PcscUltralightErrors
    {
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
                case PcscCardRefusal refusal:
                    return Classify(refusal.Code, call);

                case CardException card:
                    // One that the page operations did not translate (they all do): its code is read the same way.
                    return Classify(card.error_code(), call);

                case LibLogicalAccessException _:
                case IOException _:
                case TimeoutException _:
                case UnauthorizedAccessException _:
                case ObjectDisposedException _:
                    // A fault of the conversation with the reader and not an answer of the card: the reader is gone, the card was taken
                    // away, the Smart Card service stopped.
                    return ERROR.TransportError;

                default:
                    return ERROR.Unknown;
            }
        }

        /// <summary>
        /// A description of an exception for the log: the name of its type and, for a card refusal, the name of its error code.
        /// Never the message, which can carry what the caller passed in.
        /// </summary>
        /// <param name="exception">What was thrown.</param>
        /// <returns>The description.</returns>
        public static string Describe(Exception exception)
        {
            var type = exception.GetType().Name;

            return exception is PcscCardRefusal refusal ? type + " " + refusal.Code : type;
        }

        private static ERROR Classify(CardException.ErrorType code, UltralightCall call)
        {
            switch (code)
            {
                case CardException.ErrorType.AUTHENTICATION_FAILURE:
                case CardException.ErrorType.SECURITY_STATUS:
                case CardException.ErrorType.COMMAND_NOT_ALLOWED:
                case CardException.ErrorType.REFERENCE_KEY_UNUSABLE:
                    // The card said no. To a key that is a failed authentication, to anything else a refusal.
                    return call == UltralightCall.AuthUltralightC || call == UltralightCall.AuthPassword ? ERROR.AuthFailure : ERROR.PermissionDenied;

                case CardException.ErrorType.WRONG_LENGTH:
                case CardException.ErrorType.WRONG_P1_P2:
                case CardException.ErrorType.WRONG_LE:
                case CardException.ErrorType.WRONG_INSTRUCTION:
                case CardException.ErrorType.WRONG_CLASS:
                case CardException.ErrorType.INVALID_CLA_BYTE:
                case CardException.ErrorType.COMMAND_INCOMPATIBLE:
                case CardException.ErrorType.KEY_NUMBER_INVALID:
                case CardException.ErrorType.FUNCTION_NOT_SUPPORTED:
                    // The call does not fit the card or the reader, and the card was not asked to do anything it could refuse.
                    return ERROR.ProtocolConstraint;

                default:
                    // A card that failed to do what it was asked (execution error, memory failure), a thing that was not found, a code
                    // that LibLogicalAccess adds later: not guessed at.
                    return ERROR.Unknown;
            }
        }
    }
}
