using Microsoft.Win32;
using RFiDGear.UI.MVVMDialogs.Presenters.Interfaces;
using RFiDGear.UI.MVVMDialogs.ViewModels;

namespace RFiDGear.UI.MVVMDialogs.Presenters
{
    public class SaveFileDialogPresenter : IDialogBoxPresenter<SaveFileDialogViewModel>
    {
        public void Show(SaveFileDialogViewModel vm)
        {
            var dlg = new SaveFileDialog
            {
                FileName = vm.FileName,
                Filter = vm.Filter,
                InitialDirectory = vm.InitialDirectory,
                RestoreDirectory = vm.RestoreDirectory,
                Title = vm.Title,
                ValidateNames = vm.ValidateNames
            };

            var result = dlg.ShowDialog(vm.ParentWindow);
            vm.Result = result != null && result.Value;

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