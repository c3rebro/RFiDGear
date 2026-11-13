#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using RFiDGear.Bridge;

namespace RFiDGear.Services.ExternalControl
{
    /// <summary>
    /// What the contract says about itself, read from its attributes: which capability each method and each event belongs to.
    /// The list that <c>hello</c> answers is built from this and from nothing else, so a capability can only be advertised
    /// when a method of the contract carries it, and a method cannot be called when the host does not offer its capability.
    /// </summary>
    internal static class BridgeCatalog
    {
        /// <summary>Gets the capability of each method of <see cref="IBridgeService"/>, by its wire name.</summary>
        internal static IReadOnlyDictionary<string, string> MethodCapabilities { get; } = Read(typeof(IBridgeService));

        /// <summary>Gets the capability of each event of <see cref="IBridgeClientEvents"/>, by its wire name.</summary>
        internal static IReadOnlyDictionary<string, string> EventCapabilities { get; } = Read(typeof(IBridgeClientEvents));

        /// <summary>
        /// Gets every capability that a method or an event of the contract carries, and the guarantees of
        /// <see cref="BridgeCapabilities.Guarantees"/>, in the order of <see cref="BridgeCapabilities.All"/>.
        /// </summary>
        internal static IReadOnlyList<string> AllCapabilities { get; } = BridgeCapabilities.All
            .Where(name => MethodCapabilities.Values.Contains(name, StringComparer.Ordinal)
                || EventCapabilities.Values.Contains(name, StringComparer.Ordinal)
                || BridgeCapabilities.Guarantees.Contains(name, StringComparer.Ordinal))
            .ToList();

        /// <summary>Makes the wire name of a method: the camel-case of its name, which is also what StreamJsonRpc is told to use.</summary>
        /// <param name="methodName">The name of the method in the contract.</param>
        /// <returns>The name on the wire.</returns>
        internal static string WireName(string methodName) => char.ToLowerInvariant(methodName[0]) + methodName[1..];

        private static IReadOnlyDictionary<string, string> Read(Type contract) =>
            contract.GetMethods().ToDictionary(
                method => WireName(method.Name),
                method => (method.GetCustomAttribute<CapabilityAttribute>() ?? throw new InvalidOperationException($"{contract.Name}.{method.Name} has no capability.")).Name,
                StringComparer.Ordinal);
    }
}
