using System.Windows;
using RFiDGear.UI.MVVMDialogs.Presenters.Interfaces;
using RFiDGear.UI.MVVMDialogs.ViewModels;

namespace RFiDGear.UI.MVVMDialogs.Presenters
{
    public class MessageBoxPresenter : IDialogBoxPresenter<MessageBoxViewModel>
    {
        public void Show(MessageBoxViewModel vm)
        {
            vm.Result = MessageBox.Show(vm.ParentWindow, vm.Message, vm.Caption, vm.Buttons, vm.Image);
        }
    }
}