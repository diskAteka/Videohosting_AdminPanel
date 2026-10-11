using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http;
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

                // Применяем адрес и порт ко всем API-запросам
                _apiService.SetBaseUrl(ServerAddress, Port);
                var baseUrl = AppConfig.BuildBaseUrl(ServerAddress, Port);

                // Получаем токен через сервис аутентификации
                var loginResponse = await _authService.LoginAsync(baseUrl, UserName, Password);

                if (string.IsNullOrEmpty(loginResponse.Token))
                {
                    ShowError("Сервер не вернул токен авторизации");
                    return;
                }

                // Сохраняем токен в обработчик
                _tokenHandler.SetToken(loginResponse.Token);

                // Открываем главное окно
                var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
                mainWindow.Show();

                // Закрываем окно входа
                CloseWindow();
            }
            catch (HttpRequestException ex)
            {
                ShowError($"Ошибка подключения к серверу: {ex.Message}");
            }
            catch (UnauthorizedAccessException)
            {
                ShowError("Неверное имя пользователя или пароль");
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