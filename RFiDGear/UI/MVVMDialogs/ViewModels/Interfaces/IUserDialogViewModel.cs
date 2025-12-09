using System;

namespace RFiDGear.UI.MVVMDialogs.ViewModels.Interfaces
{
    public interface IUserDialogViewModel : IDialogViewModel
    {
        bool IsModal { get; }

        void RequestClose();

        event EventHandler DialogClosing;
    }
}
