using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;

using CommunityToolkit.Mvvm.ComponentModel;

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
        public Window ParentWindow { get; set; }
        public bool IsModal { get; set;  }

        public bool Show(IList<IDialogViewModel> collection)
        {
            collection.Add(this);          
            return Result;   
        }

        #region INotifyPropertyChanged Members

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion INotifyPropertyChanged Members
    }
}