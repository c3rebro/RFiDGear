#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

using RFiDGear.Bridge;
using RFiDGear.Contracts;

namespace RFiDGear.Services.ExternalControl
{
    /// <summary>
    /// What a <c>startRun</c> asks for, checked and in a form that can be compared: the variables without regard to their order
    /// (but with the spelling of their names, because the tasks substitute a placeholder only where it is spelled as the name
    /// is, so <c>$jobnumber</c> and <c>$JOBNUMBER</c> do different things), and the expectations about the project. Two
    /// requests with the same id are the same request exactly when their plans have the same hash. A request that breaks a rule
    /// is refused with the name of the field and never its value.
    /// </summary>
    internal sealed class RunPlan
    {
        internal RunPlan(string requestId, IReadOnlyList<KeyValuePair<string, string>> variables, string? expectProjectPath, int? expectTaskCount)
        {
            RequestId = requestId;
            Variables = variables;
            ExpectProjectPath = expectProjectPath;
            ExpectTaskCount = expectTaskCount;
            PayloadHash = Hash(variables, expectProjectPath, expectTaskCount);
        }

        /// <summary>Gets the request id.</summary>
        internal string RequestId { get; }

        /// <summary>Gets the variables of the run.</summary>
        internal IReadOnlyList<KeyValuePair<string, string>> Variables { get; }

        /// <summary>Gets the full path of the project the client expects, or <see langword="null"/>.</summary>
        internal string? ExpectProjectPath { get; }

        /// <summary>Gets the number of tasks the client expects, or <see langword="null"/>.</summary>
        internal int? ExpectTaskCount { get; }

        /// <summary>Gets the hash by which two requests with one id are told to be the same or not.</summary>
        internal string PayloadHash { get; }

        /// <summary>Checks a request and makes the plan.</summary>
        /// <param name="request">The request.</param>
        /// <returns>The plan.</returns>
        internal static RunPlan From(StartRunRequest? request)
        {
            if (request == null)
            {
                throw BridgeFault.InvalidRequest("request");
            }

            if (!BridgeProtocol.IsValidRequestId(request.RequestId))
            {
                throw BridgeFault.InvalidRequest("requestId");
            }

            var variables = CheckVariables(request.Variables);
            var expectedPath = CheckExpectedPath(request.ExpectProjectPath);

            if (request.ExpectTaskCount < 0)
            {
                throw BridgeFault.InvalidRequest("expectTaskCount");
            }

            return new RunPlan(request.RequestId, variables, expectedPath, request.ExpectTaskCount);
        }

        /// <summary>Makes the request the host is given.</summary>
        /// <returns>The request.</returns>
        internal ExecutionRequest ToExecutionRequest() => new ExecutionRequest(Variables);

        private static List<KeyValuePair<string, string>> CheckVariables(IReadOnlyList<VariablePair>? pairs)
        {
            var result = new List<KeyValuePair<string, string>>();

            if (pairs == null)
            {
                return result;
            }

            if (pairs.Count > BridgeProtocol.MaxVariables)
            {
                throw BridgeFault.InvalidRequest("variables");
            }

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var pair in pairs)
            {
                var name = pair?.Name;
                var value = pair?.Value;

                // Names are the text the tasks look for: no control characters, and no name twice, also in another case (the host
                // keeps its variables in a case-insensitive dictionary and would silently keep the last). Values may be anything
                // but a NUL.
                if (string.IsNullOrWhiteSpace(name)
                    || name.Length > BridgeProtocol.MaxVariableNameLength
                    || name.Any(char.IsControl)
                    || value == null
                    || value.Length > BridgeProtocol.MaxVariableValueLength
                    || value.Contains('\0')
                    || !seen.Add(name))
                {
                    throw BridgeFault.InvalidRequest("variables");
                }

                result.Add(new KeyValuePair<string, string>(name, value));
            }

            return result;
        }

        private static string? CheckExpectedPath(string? path)
        {
            if (path == null)
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(path) || path.Length > BridgeProtocol.MaxPathLength || path.Contains('\0') || !Path.IsPathFullyQualified(path))
            {
                throw BridgeFault.InvalidRequest("expectProjectPath");
            }

            try
            {
                return Path.GetFullPath(path);
            }
            catch (Exception e) when (e is ArgumentException || e is NotSupportedException || e is PathTooLongException || e is System.Security.SecurityException)
            {
                throw BridgeFault.InvalidRequest("expectProjectPath");
            }
        }

        // SHA-256 over length-prefixed UTF-8, so no two different plans can write the same bytes: the variables sorted by their
        // names (as spelled), then the expectations, each with a marker for "not given".
        private static string Hash(IReadOnlyList<KeyValuePair<string, string>> variables, string? expectProjectPath, int? expectTaskCount)
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);

            writer.Write(variables.Count);

            foreach (var pair in variables.OrderBy(v => v.Key, StringComparer.Ordinal))
            {
                writer.Write(pair.Key);
                writer.Write(pair.Value);
            }

            writer.Write(expectProjectPath != null);
            writer.Write(expectProjectPath?.ToUpperInvariant() ?? string.Empty);
            writer.Write(expectTaskCount.HasValue);
            writer.Write(expectTaskCount ?? 0);
            writer.Flush();

            return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
        }
    }
}
