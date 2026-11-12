#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RFiDGear.Contracts;
using RFiDGear.Infrastructure;
using RFiDGear.Models;
using Serilog;

namespace RFiDGear.Services.SecureKeys
{
    /// <summary>
    /// Gets all the keys a command needs before the command touches the card. An embedded key is passed on as it is; the key
    /// of a bound slot is asked for from its provider. Whatever goes wrong, the answer is a failure and the command makes no
    /// card call: a missing key is never replaced by another key, by an embedded key of the same slot or by the key an
    /// earlier task used.
    /// </summary>
    internal static class KeyAcquisition
    {
        internal const string NoKeyContext = "NoKeyContext";

        /// <summary>Maps the key type of the application to the algorithm a provider is told about.</summary>
        /// <param name="keyType">The key type.</param>
        /// <returns>The algorithm.</returns>
        internal static SecureKeyAlgorithm AlgorithmOf(DESFireKeyType keyType) => keyType switch
        {
            DESFireKeyType.DF_KEY_DES => SecureKeyAlgorithm.Des,
            DESFireKeyType.DF_KEY_3K3DES => SecureKeyAlgorithm.TripleDes3K,
            DESFireKeyType.DF_KEY_AES => SecureKeyAlgorithm.Aes128,
            _ => throw new ArgumentOutOfRangeException(nameof(keyType), "Not a key type of a DESFire task."),
        };

        /// <summary>
        /// Gets the keys. The bindings of the task are checked first, all of them: a binding that is wrong stops the task even
        /// when this command does not use its slot, because a typo in a slot id would otherwise leave the embedded key in
        /// use that the person meant to replace. Then every bound slot is asked for, one after the other, and all of them
        /// have to answer before the command goes on. Each slot is asked for once, however often the command uses the key.
        /// </summary>
        /// <param name="needs">The keys the command works with.</param>
        /// <param name="bindings">The key bindings of the task.</param>
        /// <param name="knownSlots">The slots of the task; a binding for another slot is wrong.</param>
        /// <param name="embeddedKeyOf">Returns the embedded key of a slot that has no binding.</param>
        /// <param name="context">What is needed to ask a provider; <c>null</c> when nothing sets it up, which fails every bound slot.</param>
        /// <returns>The keys, or the reason there are none.</returns>
        internal static async Task<AcquiredKeys> AcquireAsync(
            IReadOnlyList<KeyNeed> needs,
            IReadOnlyList<KeyBindingEntry> bindings,
            IReadOnlyCollection<string> knownSlots,
            Func<string, string?> embeddedKeyOf,
            KeyResolutionContext? context)
        {
            var issues = KeyBindingValidator.Check(bindings, knownSlots);

            if (issues.Count > 0)
            {
                return Fail(context, issues[0].Slot, string.Empty, issues[0].Problem.ToString());
            }

            var hex = new Dictionary<string, string?>(StringComparer.Ordinal);
            var bound = new HashSet<string>(StringComparer.Ordinal);
            var started = false;

            try
            {
                foreach (var need in needs)
                {
                    if (hex.ContainsKey(need.Slot))
                    {
                        continue;
                    }

                    var binding = KeyBindingList.Find(bindings, need.Slot);

                    if (binding == null)
                    {
                        hex[need.Slot] = embeddedKeyOf(need.Slot);
                        continue;
                    }

                    if (context == null)
                    {
                        return Fail(context, need.Slot, binding.ProviderId, NoKeyContext);
                    }

                    // A run that was cancelled asks for no more keys.
                    if (context.CancellationToken.IsCancellationRequested)
                    {
                        return Fail(context, need.Slot, binding.ProviderId, SecureKeyFailureReason.Cancelled.ToString());
                    }

                    if (!started)
                    {
                        started = true;
                        RunHook(context.ResolutionStarted);
                    }

                    var (key, cause) = await ResolveOneAsync(need, binding, context);

                    if (key == null)
                    {
                        return Fail(context, need.Slot, binding.ProviderId, cause);
                    }

                    // The run was cancelled while the provider answered: the command must go on to no card call.
                    if (context.CancellationToken.IsCancellationRequested)
                    {
                        return Fail(context, need.Slot, binding.ProviderId, SecureKeyFailureReason.Cancelled.ToString());
                    }

                    hex[need.Slot] = key;
                    bound.Add(need.Slot);
                }
            }
            finally
            {
                if (started)
                {
                    RunHook(context!.ResolutionFinished);
                }
            }

            return AcquiredKeys.Obtained(hex, bound);
        }

        private static async Task<(string? Hex, string Cause)> ResolveOneAsync(KeyNeed need, KeyBindingEntry binding, KeyResolutionContext context)
        {
            var request = new SecureKeyRequest
            {
                SlotId = need.Slot,
                Reference = binding.Reference,
                Algorithm = AlgorithmOf(need.KeyType),
                Usage = need.Usage,
                Scope = new SecureKeyScope { ApplicationId = need.ApplicationId, KeyNumber = need.KeyNumber },
                RunId = context.RunId,
                TaskIndex = context.TaskIndex,
                CardUid = context.CardUid,
                Interactive = context.Interactive,
            };

            // The continuation goes back to where the command runs: it writes properties that the screen is bound to.
            using var answer = await context.Registry.ResolveAsync(binding.ProviderId, request, context.CancellationToken);

            if (!answer.IsResolved)
            {
                return (null, answer.FailureReason!.Value.ToString());
            }

            if (answer.Key is not SecureKeyLease lease)
            {
                return (null, SecureKeyFailureReason.Unsupported.ToString());
            }

            // The provider answers for the algorithm it was told; a key of another length is not this key.
            if (lease.Length * 2 != CustomConverter.GetExpectedKeyHexLength(need.KeyType))
            {
                return (null, SecureKeyFailureReason.InvalidKey.ToString());
            }

            // A provider may hand over a lease that is disposed already; it has no key to give, whatever its length says.
            try
            {
                return (lease.ToHexString(), string.Empty);
            }
            catch (ObjectDisposedException)
            {
                return (null, SecureKeyFailureReason.InvalidKey.ToString());
            }
        }

        private static AcquiredKeys Fail(KeyResolutionContext? context, string slot, string providerId, string cause)
        {
            // Slot, provider and cause only: the reference names a secret, and the exception of a provider never gets here.
            Log.ForContext(typeof(KeyAcquisition)).Warning(
                "Task {TaskIndex} of run {RunId} cannot get the key of slot {Slot} from provider {ProviderId}: {Cause}",
                context?.TaskIndex ?? string.Empty,
                context?.RunId,
                slot,
                providerId,
                cause);

            return AcquiredKeys.Unavailable(new KeyFailure(slot, providerId, cause));
        }

        private static void RunHook(Action? hook)
        {
            try
            {
                hook?.Invoke();
            }
            catch (Exception e)
            {
                Log.ForContext(typeof(KeyAcquisition)).Warning("A hook around the key resolution failed ({ExceptionType})", e.GetType().FullName);
            }
        }
    }
}
