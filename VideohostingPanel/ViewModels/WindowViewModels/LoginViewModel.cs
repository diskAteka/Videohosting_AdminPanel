using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using WpfApiClient.Config;
using VideohostingPanel.Services;
using VideohostingPanel.ViewModels.Base;
using VideohostingPanel.Views.Windows;
using Application = System.Windows.Application;

namespace VideohostingPanel.ViewModels.WindowViewModels
{
    public partial class LoginViewModel : BaseViewModel
    {
        private readonly AuthService _authService;
        private readonly ApiService _apiService;
        private readonly AuthTokenHandler _tokenHandler;
        private readonly IServiceProvider _serviceProvider;

        [ObservableProperty]
        private string _serverAddress = AppConfig.DEFAULT_SERVER;
        [ObservableProperty]
        private string _port = AppConfig.DEFAULT_PORT;

        [ObservableProperty]
        private string _userName = string.Empty;

        [ObservableProperty]
        private string _password = string.Empty;

        [ObservableProperty]
        private bool _isErrorVisible;

        [ObservableProperty]
        private string _errorMessage = string.Empty;

        public LoginViewModel(
            AuthService authService,
            ApiService apiService,
            AuthTokenHandler tokenHandler,
            IServiceProvider serviceProvider)
        {
            _authService = authService;
            _apiService = apiService;
            _tokenHandler = tokenHandler;
            _serviceProvider = serviceProvider;
            Title = "Вход в систему";
        }

        [RelayCommand]
        private async Task Login()
        {
            try
            {
                IsBusy = true;
                IsErrorVisible = false;

                _apiService.SetBaseUrl(ServerAddress, Port);
                var baseUrl = AppConfig.BuildBaseUrl(ServerAddress, Port);

                var loginResponse = await _authService.LoginAsync(baseUrl, UserName, Password);

                if (string.IsNullOrEmpty(loginResponse.Token))
                {
                    ShowError("Сервер не вернул токен авторизации");
                    return;
                }

                _tokenHandler.SetToken(loginResponse.Token);

                var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
                mainWindow.Show();

                CloseWindow();
            }
            catch (LoginException ex)
            {
                ShowError(ex.Message);
            }
            catch (Exception ex)
            {
                ShowError($"Ошибка входа: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void ShowError(string message)
        {
            ErrorMessage = message;
            IsErrorVisible = true;
        }

        private void CloseWindow()
        {
            Application.Current.Windows
                .OfType<LoginWindow>()
                .FirstOrDefault()?
                .Close();
        }
    }
}