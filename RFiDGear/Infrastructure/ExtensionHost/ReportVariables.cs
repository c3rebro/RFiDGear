#nullable enable
using RFiDGear.Services;

using System;
using System.Collections.Generic;
using System.IO;

namespace RFiDGear.Infrastructure.ExtensionHost
{
    /// <summary>
    /// Reads the report variables of a run (<c>REPORTTARGETPATH</c>, <c>REPORTOUTPUTPATH</c>, <c>REPORTTEMPLATEFILE</c>,
    /// <c>REPORTTEMPLATEPATH</c>) the way the start-up arguments do, so that every way to start a run (an extension, the bridge, the
    /// card of an approval in automatic mode) means the same by them. The execution service reads the report paths from the fields
    /// of the request, not from the variable dictionary, so the variables have to be turned into those fields first.
    /// </summary>
    internal static class ReportVariables
    {
        /// <summary>
        /// Applies the same interpretation as the startup arguments: the report variables set the request's
        /// <c>ReportOutputPath</c> / <c>ReportTemplateFile</c>. Blank values count as not supplied.
        /// </summary>
        /// <param name="variables">The variables of the run; names are compared as the dictionary compares them.</param>
        /// <param name="reportOutputPath">The report file to create (a <c>?</c> placeholder resolved to the next free number), or <see langword="null"/> when no variable names one.</param>
        /// <param name="reportTemplateFile">The report template, or <see langword="null"/> when no variable names one.</param>
        /// <exception cref="ArgumentException">A supplied report variable cannot be honoured.</exception>
        internal static void Resolve(
            IReadOnlyDictionary<string, string> variables,
            out string? reportOutputPath,
            out string? reportTemplateFile)
        {
            reportOutputPath = null;
            reportTemplateFile = null;

            if (TryGetFirstNonBlank(variables, out var targetKey, out var target, "REPORTTARGETPATH", "REPORTOUTPUTPATH"))
            {
                reportOutputPath = ResolveReportTarget(targetKey, target);
            }

            if (TryGetFirstNonBlank(variables, out var templateKey, out var template, "REPORTTEMPLATEFILE", "REPORTTEMPLATEPATH"))
            {
                if (!File.Exists(template))
                {
                    throw new ArgumentException($"Variable {templateKey} must name an existing file.", "request");
                }

                reportTemplateFile = template;
            }
        }

        /// <summary>
        /// Validates a report target before the run is accepted: it must be a fully qualified path that names a
        /// file (not a folder) in a folder that exists. <see cref="StartupArgumentProcessor.NormalizeReportTargetPath"/>
        /// only checks the folder, so an existing folder or a path ending in a separator would pass it and the
        /// run would perform card operations before the report step discovered it cannot write anything.
        /// </summary>
        /// <returns>The path to create, with any <c>?</c> placeholder resolved to the next free number.</returns>
        /// <exception cref="ArgumentException">The target cannot be used as a report file.</exception>
        private static string ResolveReportTarget(string key, string target)
        {
            try
            {
                if (!Path.IsPathFullyQualified(target))
                {
                    throw new ArgumentException($"Variable {key} must be a full (absolute) file path.", "request");
                }

                var resolved = StartupArgumentProcessor.NormalizeReportTargetPath(target)
                    ?? throw new ArgumentException($"Variable {key} must be a file path whose folder exists.", "request");

                if (string.IsNullOrEmpty(Path.GetFileName(resolved)) || Directory.Exists(resolved))
                {
                    throw new ArgumentException($"Variable {key} must name a file, not a folder.", "request");
                }

                return resolved;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is NotSupportedException)
            {
                // Looking at the folder failed (unreadable, name too long, ...): the target cannot be used either.
                throw new ArgumentException($"Variable {key} cannot be used as a report file path.", "request");
            }
        }

        private static bool TryGetFirstNonBlank(
            IReadOnlyDictionary<string, string> variables,
            out string key,
            out string value,
            params string[] candidateKeys)
        {
            foreach (var candidate in candidateKeys)
            {
                if (variables.TryGetValue(candidate, out var found) && !string.IsNullOrWhiteSpace(found))
                {
                    key = candidate;
                    value = found;
                    return true;
                }
            }

            key = string.Empty;
            value = string.Empty;
            return false;
        }
    }
}
