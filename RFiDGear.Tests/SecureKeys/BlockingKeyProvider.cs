using RFiDGear.Contracts;
using RFiDGear.Services.SecureKeys;

using System;
using System.Threading.Tasks;

namespace RFiDGear.Tests.SecureKeys
{
    /// <summary>A provider that holds every answer back until the test lets it go, to look at what a window does meanwhile.</summary>
    internal sealed class BlockingKeyProvider
    {
        /// <summary>The id the provider is registered under.</summary>
        internal const string Id = "test.vault";

        private readonly int _keyLength;
        private readonly TaskCompletionSource<bool> _asked = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> _gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>Creates the provider.</summary>
        /// <param name="keyLength">The length of the keys it hands out once it is released, in bytes.</param>
        internal BlockingKeyProvider(int keyLength)
        {
            _keyLength = keyLength;
        }

        /// <summary>Gets a task that completes when the provider has been asked.</summary>
        internal Task Asked => _asked.Task;

        /// <summary>Lets the provider answer.</summary>
        internal void Release() => _gate.TrySetResult(true);

        /// <summary>Makes the registry that holds only this provider.</summary>
        /// <returns>The registry.</returns>
        internal SecureKeyRegistry Registry() => SecureKeyRegistry.Create(
            new[]
            {
                new SecureKeyProviderRegistration(
                    Id,
                    "Blocking vault",
                    new ScriptedSecureKeyProvider(async (_, _) =>
                    {
                        _asked.TrySetResult(true);
                        await _gate.Task;

                        return SecureKeyResolution.Resolved(new SecureKeyLease(new byte[_keyLength]));
                    })),
            },
            Array.Empty<Lazy<ISecureKeyProvider, RFiDGear.ExtensionSdk.ISecureKeyProviderMetadata>>());
    }
}
