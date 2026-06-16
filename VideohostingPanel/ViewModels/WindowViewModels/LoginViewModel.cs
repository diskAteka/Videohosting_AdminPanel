using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http;
using VideohostingPanel.Services;
using VideohostingPanel.ViewModels.Base;
using VideohostingPanel.Views.Windows;
using Application = System.Windows.Application;

namespace VideohostingPanel.ViewModels.WindowViewModels
{
    public partial class LoginViewModel : BaseViewModel
    {
        private readonly AuthService _authService;
        private readonly AuthApiService _authApiService;
        private readonly IServiceProvider _serviceProvider;

        // Пути для тестирования (можно менять в начале класса)
        private const string DEFAULT_SERVER = "localhost";
        private const string DEFAULT_PORT = "5001";

        [ObservableProperty]
        private string _serverAddress = DEFAULT_SERVER;

        [ObservableProperty]
        private string _port = DEFAULT_PORT;

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
            AuthApiService authApiService,
            IServiceProvider serviceProvider)
        {
            _authService = authService;
            _authApiService = authApiService;
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

                // Формируем базовый URL из адреса и порта
                var baseUrl = $"https://{ServerAddress}:{Port}";

                // Настраиваем базовый URL для всех API-клиентов
                _authApiService.SetBaseUrl(ServerAddress, Port);

                // Получаем токен через сервис аутентификации
                var loginResponse = await _authService.LoginAsync(baseUrl, UserName, Password);

                if (string.IsNullOrEmpty(loginResponse.Token))
                {
                    ShowError("Сервер не вернул токен авторизации");
                    return;
                }

                // Сохраняем токен в обработчик
                _authApiService.SetToken(loginResponse.Token);

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