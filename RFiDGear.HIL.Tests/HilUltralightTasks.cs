#nullable enable
using RFiDGear.Infrastructure.Tasks;
using RFiDGear.Infrastructure.Ultralight;
using RFiDGear.ViewModel.TaskSetupViewModels;

using System;

namespace RFiDGear.HIL.Tests
{
    /// <summary>
    /// Builds the tasks of the Ultralight / NTAG hardware cases. A task is set up the way the dialog does it, with the chip it
    /// expects named or not, and never with an authentication: the cases never send a key or a password to a card (the factory
    /// password of an NTAG, <c>FFFFFFFF</c>, least of all).
    /// </summary>
    internal static class HilUltralightTasks
    {
        /// <summary>
        /// The page the write cases use. It is a page of the user memory of every chip of the family (the smallest, a plain
        /// Ultralight, has user pages 4 to 15), and not page 4, where an NTAG keeps the start of its NDEF message.
        /// </summary>
        public const int UserPage = 10;

        /// <summary>The page the refusal case writes to: the one-time-programmable bytes (the capability container of an NTAG).</summary>
        public const int ProtectedPage = 3;

        /// <summary>Builds the task that reads pages.</summary>
        /// <param name="chip">The chip the task expects.</param>
        /// <param name="startPage">The first page.</param>
        /// <param name="pageCount">The number of pages.</param>
        /// <returns>The task.</returns>
        public static MifareUltralightSetupViewModel Read(UltralightExpectation chip, int startPage, int pageCount) =>
            new MifareUltralightSetupViewModel
            {
                SelectedTaskType = TaskType_MifareUltralightTask.ReadData,
                CurrentTaskIndex = "0",
                ExpectedChip = chip,
                StartPage = startPage,
                PageCount = pageCount,
            };

        /// <summary>Builds the task that writes pages.</summary>
        /// <param name="chip">The chip the task expects.</param>
        /// <param name="startPage">The first page.</param>
        /// <param name="data">The bytes, a multiple of four.</param>
        /// <param name="allowProtectedPages">Whether the task opts in to writing protected pages.</param>
        /// <returns>The task.</returns>
        public static MifareUltralightSetupViewModel Write(UltralightExpectation chip, int startPage, byte[] data, bool allowProtectedPages = false) =>
            new MifareUltralightSetupViewModel
            {
                SelectedTaskType = TaskType_MifareUltralightTask.WriteData,
                CurrentTaskIndex = "0",
                ExpectedChip = chip,
                StartPage = startPage,
                WriteDataHex = Convert.ToHexString(data),
                AllowProtectedPages = allowProtectedPages,
            };
    }
}
