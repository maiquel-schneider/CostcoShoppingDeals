using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace CostcoApp.ViewModels
{
    /// <summary>
    /// Base class for all view models, providing INotifyPropertyChanged
    /// and a helper to set fields and raise PropertyChanged.
    /// </summary>
    public abstract class ViewModelBase : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// Sets the field to the new value and raises PropertyChanged if it changed.
        /// </summary>
        protected void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (!Equals(field, value))
            {
                field = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }
}
