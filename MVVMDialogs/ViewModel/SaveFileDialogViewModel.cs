using System.Collections.Generic;
using System.ComponentModel;

namespace MvvmDialogs.ViewModels
{
    public class SaveFileDialogViewModel : IDialogViewModel
    {
        public string FileName { get; set; }
        public string[] FileNames { get; set; }
        public string Filter { get; set; }
        public string InitialDirectory { get; set; }
        public bool RestoreDirectory { get; set; }
        public string SafeFileName { get; set; }
        public string[] SafeFileNames { get; set; }
        public string Title { get; set; }
        public bool ValidateNames { get; set; }
        public bool Result { get; set; }

        public bool Show(IList<IDialogViewModel> collection)
        {
            collection.Add(this);
            return this.Result;
        }

        #region INotifyPropertyChanged Members

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            if (this.PropertyChanged != null)
                this.PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion INotifyPropertyChanged Members
    }
}