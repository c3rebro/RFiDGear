#nullable enable
using System.ComponentModel;
using RFiDGear.Models;

namespace RFiDGear.Services.SecureKeys
{
    /// <summary>
    /// What a key slot editor needs from the task it edits. The setup view models of the DESFire and the MIFARE Classic
    /// tasks implement it. A change of the bindings is announced as a change of <c>KeyBindings</c>.
    /// </summary>
    internal interface IKeySlotHost : INotifyPropertyChanged
    {
        /// <summary>Returns whether a slot has a key binding, usable or not.</summary>
        /// <param name="slotId">The slot id; see <see cref="SecureKeySlots"/>.</param>
        /// <returns><c>true</c> when the slot is bound.</returns>
        bool IsKeyBound(string slotId);

        /// <summary>Returns a copy of the binding of a slot.</summary>
        /// <param name="slotId">The slot id.</param>
        /// <returns>The binding, or <c>null</c> when the slot has none.</returns>
        KeyBindingEntry? GetKeyBinding(string slotId);

        /// <summary>Returns whether the task holds a key of its own for the slot, that is, whether switching to a reference would discard one.</summary>
        /// <param name="slotId">The slot id.</param>
        /// <returns><c>true</c> when the embedded key is not empty.</returns>
        bool HasEmbeddedKey(string slotId);

        /// <summary>Returns how many hexadecimal digits the key of a slot has, which follows the key type the person chose for it.</summary>
        /// <param name="slotId">The slot id.</param>
        /// <returns>The number of digits.</returns>
        int ExpectedKeyHexLength(string slotId);

        /// <summary>
        /// Binds a slot to a key in a provider, replacing a binding it has. The binding is written first and the embedded key
        /// of the slot is cleared second, so the slot is never left with neither.
        /// </summary>
        /// <param name="slotId">The slot id.</param>
        /// <param name="providerId">The id of the provider.</param>
        /// <param name="reference">The reference; it may be empty or wrong while a person is typing it, and the task then fails instead of running.</param>
        void BindKeySlot(string slotId, string providerId, string reference);

        /// <summary>Removes the binding of a slot. The slot has no key then until one is typed.</summary>
        /// <param name="slotId">The slot id.</param>
        void UnbindKeySlot(string slotId);
    }
}
