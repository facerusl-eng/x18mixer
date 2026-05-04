using System.ComponentModel;

namespace WpfMixer.Core.Interfaces;

public interface INavigationService : INotifyPropertyChanged
{
    object? CurrentViewModel { get; }
    bool CanGoBack { get; }
    void NavigateTo<TViewModel>() where TViewModel : class;
    void NavigateTo(Type viewModelType);
    bool GoBack();
}
