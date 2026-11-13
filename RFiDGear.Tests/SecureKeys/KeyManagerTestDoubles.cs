#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Resources;
using System.Security;

using RFiDGear.Models;
using RFiDGear.Services.SecureKeys;
using RFiDGear.ViewModel;

namespace RFiDGear.Tests.SecureKeys
{
    /// <summary>A task with any number of key slots, which notes what it was asked and how many listen to it.</summary>
    internal sealed class FakeKeySlotHost : IKeySlotHost
    {
        private readonly List<KeyBindingEntry> _bindings = new List<KeyBindingEntry>();

        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>Gets the keys that are typed into the task, by slot.</summary>
        public Dictionary<string, string> EmbeddedKeys { get; } = new Dictionary<string, string>(StringComparer.Ordinal);

        /// <summary>Gets or sets how many digits every key has.</summary>
        public int HexLength { get; set; } = 32;

        /// <summary>Gets what the task was asked to do, in order.</summary>
        public List<string> Calls { get; } = new List<string>();

        /// <summary>Gets how many handlers listen to the task.</summary>
        public int Listeners => PropertyChanged?.GetInvocationList().Length ?? 0;

        public bool IsKeyBound(string slotId) => _bindings.Any(entry => entry.Slot == slotId);

        public KeyBindingEntry? GetKeyBinding(string slotId) => _bindings.FirstOrDefault(entry => entry.Slot == slotId)?.Clone();

        public bool HasEmbeddedKey(string slotId) => EmbeddedKeys.TryGetValue(slotId, out var key) && key.Length > 0;

        public int ExpectedKeyHexLength(string slotId) => HexLength;

        public void BindKeySlot(string slotId, string providerId, string reference)
        {
            Calls.Add($"bind {slotId} {providerId} '{reference}'");
            _bindings.RemoveAll(entry => entry.Slot == slotId);
            _bindings.Add(new KeyBindingEntry { Slot = slotId, ProviderId = providerId, Reference = reference });
            EmbeddedKeys.Remove(slotId);
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("KeyBindings"));
        }

        public void UnbindKeySlot(string slotId)
        {
            Calls.Add($"unbind {slotId}");
            _bindings.RemoveAll(entry => entry.Slot == slotId);
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("KeyBindings"));
        }
    }

    /// <summary>The person, scripted: whether they agree to discard a typed key. The manager asks for no key by dialog.</summary>
    internal sealed class ScriptedKeyDialogs : IKeySlotEditorDialogs
    {
        public bool Confirm { get; set; } = true;

        public int Confirmations { get; private set; }

        public bool ConfirmReplaceKey()
        {
            Confirmations++;

            return Confirm;
        }

        public SecureString? AskForKey(int hexLength) => throw new InvalidOperationException("The key manager has a box for the key; it never asks for it in a dialog.");

        public void Show(KeySlotMessage message, int detail) =>
            throw new InvalidOperationException("The key manager says what happened in its own window; it shows no message box.");
    }

    /// <summary>A supplier status that the test drives, and that counts who listens.</summary>
    internal sealed class FakeSupplierStatus : IKeySupplierStatus
    {
        public KeySupplierAvailability Current { get; set; } = KeySupplierAvailability.Off;

        public event EventHandler? Changed;

        public int Subscribers => Changed?.GetInvocationList().Length ?? 0;

        public void Raise() => Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>The texts of the application, from the resources, in a language, without the settings file.</summary>
    internal static class TestText
    {
        private static readonly ResourceManager Manifest =
            new ResourceManager("RFiDGear.Resources.Manifest", typeof(MainWindowViewModel).Assembly);

        public static Func<string, string> In(string culture) => key => Manifest.GetString(key, new CultureInfo(culture)) ?? string.Empty;

        public static string English(string key) => In("en")(key);

        /// <summary>Makes the entry of a key the way a password box holds it.</summary>
        public static SecureString Secure(string digits)
        {
            var secure = new SecureString();

            foreach (var digit in digits)
            {
                secure.AppendChar(digit);
            }

            secure.MakeReadOnly();

            return secure;
        }
    }
}
