using System;

namespace RFiDGear.ViewModel.DialogFactories
{
    /// <summary>
    /// Admits a change of the task list that is made from a dialog or a command of the main window. The task loop
    /// reads the list by index on every pass, so a task that is removed, added or re-sorted under it changes which
    /// task runs next (a finished task can run a second time, which breaks the one-attempt rule). The change is
    /// therefore made only while no run, load or other edit is in progress.
    /// </summary>
    public interface ITaskListEditGate
    {
        /// <summary>
        /// Runs <paramref name="edit"/> if the task list may be changed now. When it may not, <paramref name="edit"/>
        /// is not run and the user is told.
        /// </summary>
        /// <param name="edit">
        /// Makes the change. Returns <see langword="false"/> when it was not made (for example because the input was
        /// rejected), so the caller keeps its dialog open.
        /// </param>
        /// <returns>
        /// <see langword="true"/> when the task list may be changed and <paramref name="edit"/> made the change;
        /// <see langword="false"/> when it was refused or <paramref name="edit"/> did not make the change.
        /// </returns>
        bool TryEdit(Func<bool> edit);
    }
}
