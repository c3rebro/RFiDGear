using System;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace RFiDGear.Bridge
{
    /// <summary>
    /// The version of the wire protocol and the limits that go with it. A client negotiates by <see cref="Major"/> only:
    /// the minor version is informational, because the capability list in <c>hello</c> says what the host can do. A major
    /// version changes for a removal, a rename, a change of type or a change of meaning; every addition is a minor change.
    /// </summary>
    [SuppressMessage("Major Code Smell", "S2339:Public constant members should not be used", Justification = WireConstants.Justification)]
    public static class BridgeProtocol
    {
        /// <summary>The name that RFiDGear answers in <c>hello</c>; a client uses it to check it reached the right server.</summary>
        public const string Product = "RFiDGear";

        /// <summary>The major version of the protocol described by this assembly.</summary>
        public const int Major = 1;

        /// <summary>The minor version of the protocol described by this assembly.</summary>
        public const int Minor = 0;

        /// <summary>What every pipe name starts with; the user's security identifier follows.</summary>
        public const string PipeNamePrefix = "RFiDGear.Bridge.";

        /// <summary>The longest message the host accepts, in bytes. A longer one closes the connection.</summary>
        public const int MaxMessageBytes = 1024 * 1024;

        /// <summary>The most variables a run may be given.</summary>
        public const int MaxVariables = 64;

        /// <summary>The longest variable name, in characters.</summary>
        public const int MaxVariableNameLength = 128;

        /// <summary>The longest variable value, in characters.</summary>
        public const int MaxVariableValueLength = 2048;

        /// <summary>The longest path, in characters.</summary>
        public const int MaxPathLength = 4096;

        /// <summary>The longest request id, in characters.</summary>
        public const int MaxRequestIdLength = 64;

        /// <summary>How many full results of runs the host keeps for a later lookup.</summary>
        public const int ResultHistory = 64;

        /// <summary>How many request ids the host remembers, so that a repeated start is recognized.</summary>
        public const int RequestIdHistory = 1024;

        /// <summary>The longest time a lookup may wait for a result, in milliseconds.</summary>
        public const int MaxWaitMs = 30000;

        /// <summary>The characters a request id may have; it is logged, so the alphabet is small.</summary>
        public const string RequestIdPattern = @"^[A-Za-z0-9._:-]{1,64}\z";

        private static readonly Regex RequestIdExpression = new Regex(RequestIdPattern, RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);

        /// <summary>Makes the name of the pipe a given Windows user's RFiDGear listens on. The pipe namespace is machine-wide, so the user is part of the name.</summary>
        /// <param name="userSid">The security identifier of the user who runs RFiDGear, for example <c>S-1-5-21-...</c>.</param>
        /// <returns>The pipe name, without the <c>\\.\pipe\</c> prefix.</returns>
        /// <exception cref="ArgumentException">The identifier is empty.</exception>
        public static string PipeNameFor(string userSid)
        {
            if (string.IsNullOrWhiteSpace(userSid))
            {
                throw new ArgumentException("The security identifier of the user is needed.", nameof(userSid));
            }

            return PipeNamePrefix + userSid.Trim();
        }

        /// <summary>Says whether a text is a request id the host accepts.</summary>
        /// <param name="requestId">The text to check.</param>
        /// <returns><c>true</c> when it has 1 to 64 characters from <c>A-Z a-z 0-9 . _ : -</c>.</returns>
        public static bool IsValidRequestId(string? requestId) =>
            requestId != null && RequestIdExpression.IsMatch(requestId);
    }
}
