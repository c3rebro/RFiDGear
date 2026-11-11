using System;

using RFiDGear.ViewModel.DialogFactories;

namespace RFiDGear.Infrastructure.ExtensionHost
{
    /// <summary>
    /// Admits a change of the task list through the coordinator's project-edit slot, the same slot an extension needs
    /// for <c>New</c>, <c>ReplaceTaskCollection</c> and a status reset. The slot is held only while the change is made,
    /// not while a dialog is open.
    /// </summary>
    /// <remarks>
    /// The change is made from a click handler that cannot wait, so a reader scan of an extension (which is short) is not
    /// waited for: the user is told and confirms again.
    /// </remarks>
    internal sealed class CoordinatorTaskListEditGate : ITaskListEditGate
    {
        private readonly ExtensionOperationCoordinator coordinator;
        private readonly Action notifyBusy;

        /// <summary>
        /// Creates the gate.
        /// </summary>
        /// <param name="coordinator">The coordinator shared with the extension host.</param>
        /// <param name="notifyBusy">Tells the user that the task list cannot be changed right now.</param>
        public CoordinatorTaskListEditGate(ExtensionOperationCoordinator coordinator, Action notifyBusy)
        {
            this.coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
            this.notifyBusy = notifyBusy ?? throw new ArgumentNullException(nameof(notifyBusy));
        }

        /// <inheritdoc />
        public bool TryEdit(Func<bool> edit)
        {
            if (edit == null)
            {
                throw new ArgumentNullException(nameof(edit));
            }

            if (!coordinator.TryBeginProjectOperation())
            {
                notifyBusy();
                return false;
            }

            try
            {
                return edit();
            }
            finally
            {
                coordinator.EndProjectOperation();
            }
        }
    }
}
