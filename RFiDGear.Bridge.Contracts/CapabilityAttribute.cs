using System;

namespace RFiDGear.Bridge
{
    /// <summary>
    /// Names the capability that a method or an event belongs to. The host builds the capability list that it answers in
    /// <c>hello</c> from these attributes, so a feature cannot be advertised without a method that uses it, and a method
    /// cannot exist without a capability a client can check. Every method of <see cref="IBridgeService"/> and
    /// <see cref="IBridgeClientEvents"/> carries exactly one.
    /// <para>
    /// A capability appears only when the feature is complete, is never removed or given another meaning within a protocol
    /// major version, and the names are listed in <see cref="BridgeCapabilities"/>.
    /// </para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public sealed class CapabilityAttribute : Attribute
    {
        /// <summary>Creates the attribute.</summary>
        /// <param name="name">The capability name, one of <see cref="BridgeCapabilities"/>.</param>
        public CapabilityAttribute(string name)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
        }

        /// <summary>Gets the capability name.</summary>
        public string Name { get; }
    }
}
