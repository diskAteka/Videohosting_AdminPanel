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

        private const string WPF_PREFIX = "/wpf";

        [ObservableProperty]
        private BaseViewModel? _currentView;

        public MainViewModel(
            ApiService apiService,
            AuthApiService authApiService,
            IServiceProvider serviceProvider)
        {
            _apiService = apiService;
            _authApiService = authApiService;
            _serviceProvider = serviceProvider;
            Title = "Панель администратора";
        }

        [RelayCommand]
        private void Logout()
        {
            // Сбрасываем токен
            _authApiService.ClearAuth();

            // Открываем окно входа
            var loginWindow = _serviceProvider.GetRequiredService<LoginWindow>();
            loginWindow.Show();

            // Закрываем главное окно
            Application.Current.Windows
                .OfType<MainWindow>()
                .FirstOrDefault()?
                .Close();
        }
    }
}