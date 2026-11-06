using System;

namespace RFiDGear.ExtensionLibraries.CardCheck
{
    /// <summary>
    /// Why an operation did not succeed, in a form that is safe to show and to log. It never carries a server
    /// name, a user name, a connection string, a path outside the report folder or any file content: the code is a
    /// short stable word, the message is fixed text chosen by this library.
    /// </summary>
    public sealed record Failure
    {
        /// <summary>Creates a failure.</summary>
        /// <param name="kind">What kind of problem it is.</param>
        /// <param name="code">A short stable identifier such as <c>order.notFound</c>; must not be empty.</param>
        /// <param name="safeMessage">Fixed text that is safe to show; <c>null</c> becomes empty.</param>
        /// <exception cref="ArgumentException"><paramref name="code"/> is null, empty or whitespace.</exception>
        public Failure(FailureKind kind, string code, string? safeMessage = null)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                throw new ArgumentException("A failure needs a code.", nameof(code));
            }

            Kind = kind;
            Code = code;
            SafeMessage = safeMessage ?? string.Empty;
        }

        /// <summary>What kind of problem it is.</summary>
        public FailureKind Kind { get; }

        /// <summary>A short stable identifier, for tests, logs and mapping to localized text.</summary>
        public string Code { get; }

        /// <summary>Fixed text that is safe to show or log.</summary>
        public string SafeMessage { get; }
    }
}
