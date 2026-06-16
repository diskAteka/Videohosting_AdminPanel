using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using VideohostingPanel.Services;
using VideohostingPanel.ViewModels.Base;
using VideohostingPanel.Views.Windows;
using Application = System.Windows.Application;

namespace VideohostingPanel.ViewModels.WindowViewModels
{
    public partial class MainViewModel : BaseViewModel
    {
        private readonly ApiService _apiService;
        private readonly AuthApiService _authApiService;
        private readonly IServiceProvider _serviceProvider;
        private readonly AuthTokenHandler _tokenHandler;

        private const string WPF_PREFIX = "/wpf";

        [ObservableProperty]
        private BaseViewModel? _currentView;

        public MainViewModel(
            ApiService apiService,
            AuthApiService authApiService,
            IServiceProvider serviceProvider,
            AuthTokenHandler tokenHandler)
        {
            _apiService = apiService;
            _authApiService = authApiService;
            _serviceProvider = serviceProvider;
            Title = "Панель администратора";
            _tokenHandler = tokenHandler;
            _tokenHandler.OnTokenExpired += () => { Application.Current.Dispatcher.BeginInvoke(() => Logout()); };
        }

        [RelayCommand]
        private void Logout()
        {
            _authApiService.ClearAuth();
            var loginWindow = _serviceProvider.GetRequiredService<LoginWindow>();
            loginWindow.Show();
            Application.Current.Windows.OfType<MainWindow>().FirstOrDefault()?.Close();
        }
    }
}