using Microsoft.Win32;
using MVVMDialogs.Presenters.Interfaces;
using MVVMDialogs.ViewModels;

namespace MVVMDialogs.Presenters
{
    public class OpenFileDialogPresenter : IDialogBoxPresenter<OpenFileDialogViewModel>
    {
        public void Show(OpenFileDialogViewModel vm)
        {
            var dlg = new OpenFileDialog
            {
                Multiselect = vm.Multiselect,
                ReadOnlyChecked = vm.ReadOnlyChecked,
                ShowReadOnly = vm.ShowReadOnly,
                FileName = vm.FileName,
                Filter = vm.Filter,
                InitialDirectory = vm.InitialDirectory,
                RestoreDirectory = vm.RestoreDirectory,
                Title = vm.Title,
                ValidateNames = vm.ValidateNames
            };

            var result = dlg.ShowDialog();
            vm.Result = (result != null) && result.Value;

            vm.Multiselect = dlg.Multiselect;
            vm.ReadOnlyChecked = dlg.ReadOnlyChecked;
            vm.ShowReadOnly = dlg.ShowReadOnly;
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