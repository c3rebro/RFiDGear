using System.Windows;
using MVVMDialogs.ViewModels;

namespace MVVMDialogs.Presenters
{
    public class MessageBoxPresenter : IDialogBoxPresenter<MessageBoxViewModel>
    {
        public void Show(MessageBoxViewModel vm)
        {
            vm.Result = MessageBox.Show(vm.ParentWindow, vm.Message, vm.Caption, vm.Buttons, vm.Image);
        }
    }
}