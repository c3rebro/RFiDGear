using RFiDGear.UI.MVVMDialogs.ViewModels.Interfaces;

namespace RFiDGear.UI.MVVMDialogs.Presenters.Interfaces
{
    public interface IDialogBoxPresenter<T> where T : IDialogViewModel
    {
        void Show(T viewModel);
    }
}
