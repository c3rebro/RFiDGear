#nullable enable
using System;
using System.Collections.Generic;

namespace RFiDGear.Tests.ApiSurface
{
    /// <summary>
    /// What <see cref="ApiSurfaceWriter"/> treats as the assembly's own vocabulary and as surface.
    /// </summary>
    /// <param name="ElidedNamespaces">
    /// Namespaces whose types are written without their namespace when they appear in a signature, to keep the
    /// snapshot readable. A type's own declaration line always carries its full name.
    /// </param>
    /// <param name="ExtraSurfaceAttributes">
    /// Attribute types, besides <c>Obsolete</c>, <c>Flags</c> and any attribute defined in an <c>RFiDGear*</c>
    /// namespace, that are part of the surface because removing or changing them changes how the member behaves.
    /// For the extension SDK that is <c>[MetadataAttribute]</c>: without it every <c>[UiExtension]</c> would silently
    /// lose its MEF metadata.
    /// </param>
    internal sealed record ApiSurfaceOptions(
        IReadOnlySet<string> ElidedNamespaces,
        IReadOnlySet<Type> ExtraSurfaceAttributes)
    {
        /// <summary>The options that describe <c>RFiDGear.Contracts</c>. Changing them changes its snapshot.</summary>
        public static ApiSurfaceOptions Default { get; } = new(
            new HashSet<string>(StringComparer.Ordinal) { "RFiDGear.Contracts" },
            new HashSet<Type>());
    }
}
