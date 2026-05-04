using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using WpfMixer.Core.Interfaces;

namespace WpfMixer.Core.Services;

public sealed partial class NavigationService : ObservableObject, INavigationService
{
    private readonly IServiceProvider _provider;
    private readonly Stack<object> _backStack = new();

    [ObservableProperty]
    private object? _currentViewModel;

    public NavigationService(IServiceProvider provider)
    {
        _provider = provider;
    }

    public bool CanGoBack => _backStack.Count > 0;

    public void NavigateTo<TViewModel>() where TViewModel : class
    {
        NavigateTo(typeof(TViewModel));
    }

    public void NavigateTo(Type viewModelType)
    {
        var vm = _provider.GetRequiredService(viewModelType);
        if (CurrentViewModel is not null)
            _backStack.Push(CurrentViewModel);

        CurrentViewModel = vm;
        OnPropertyChanged(new PropertyChangedEventArgs(nameof(CanGoBack)));
    }

    public bool GoBack()
    {
        if (_backStack.Count == 0) return false;
        CurrentViewModel = _backStack.Pop();
        OnPropertyChanged(new PropertyChangedEventArgs(nameof(CanGoBack)));
        return true;
    }
}
