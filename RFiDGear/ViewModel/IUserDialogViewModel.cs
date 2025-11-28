using System;

namespace MVVMDialogs.ViewModels
{
    public interface IUserDialogViewModel : IDialogViewModel
    {
        bool IsModal { get; }

        void RequestClose();

        event EventHandler DialogClosing;
    }
}