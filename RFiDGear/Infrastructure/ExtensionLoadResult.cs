#nullable enable

using System;

namespace RFiDGear.Infrastructure
{
    /// <summary>
    /// What happened to one extension assembly while the MEF container was composed.
    /// </summary>
    /// <param name="AssemblyPath">The full path of the extension assembly that was considered.</param>
    /// <param name="Loaded">
    /// True when the assembly's parts are part of the composed container; false when it was skipped.
    /// </param>
    /// <param name="PartCount">The number of MEF parts found in the assembly (0 when it was skipped).</param>
    /// <param name="Error">Why the assembly was skipped, or null when it was loaded.</param>
    internal sealed record ExtensionLoadResult(string AssemblyPath, bool Loaded, int PartCount, Exception? Error);
}
