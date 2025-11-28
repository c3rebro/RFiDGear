using Microsoft.Win32;
using MVVMDialogs.Presenters.Interfaces;
using MVVMDialogs.ViewModels;

namespace MVVMDialogs.Presenters
{
    public class SaveFileDialogPresenter : IDialogBoxPresenter<SaveFileDialogViewModel>
    {
        public void Show(SaveFileDialogViewModel vm)
        {
            var dlg = new SaveFileDialog();

            dlg.FileName = vm.FileName;
            dlg.Filter = vm.Filter;
            dlg.InitialDirectory = vm.InitialDirectory;
            dlg.RestoreDirectory = vm.RestoreDirectory;
            dlg.Title = vm.Title;
            dlg.ValidateNames = vm.ValidateNames;
            
            var result = dlg.ShowDialog(vm.ParentWindow);
            vm.Result = (result != null) && result.Value;

            vm.FileName = dlg.FileName;
            vm.FileNames = dlg.FileNames;
            vm.Filter = dlg.Filter;
            vm.InitialDirectory = dlg.InitialDirectory;
            vm.RestoreDirectory = dlg.RestoreDirectory;
            vm.SafeFileName = dlg.SafeFileName;
            vm.SafeFileNames = dlg.SafeFileNames;
            vm.Title = dlg.Title;
            vm.ValidateNames = dlg.ValidateNames;
        }
    }
}