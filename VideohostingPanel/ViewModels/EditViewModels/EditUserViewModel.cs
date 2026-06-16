using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SharedLib.Contracts.Admin;
using SharedLib.Models;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Windows;
using VideohostingPanel.Resources.Elements;
using VideohostingPanel.Services;
using VideohostingPanel.ViewModels.Base;
using VideohostingPanel.Views.Windows;
using WpfApiClient.Config;

namespace VideohostingPanel.ViewModels.EditViewModels
{
    public partial class EditUserViewModel : BaseViewModel
    {
        private ApiService _apiService;
        private string emailRegex = AppConfig.EMAIL_REGEX;

        #region Свйоства для разметки

        [ObservableProperty]
        private GetUserResponse? _user;

        [ObservableProperty]
        private bool _isEditMode;

        [ObservableProperty]
        private string _password = string.Empty;

        [ObservableProperty]
        private DateTime _registerDateSelected = DateTime.UtcNow;

        [ObservableProperty]
        private string _registerTime = DateTime.Now.ToString("HH:mm");

        #endregion

        #region Команды

        [RelayCommand]
        private async Task SaveChanges()
        {
            try
            {
                IsBusy = true;
                if (string.IsNullOrEmpty(User.Name))
                {
                    ModernMessageBox.Show("Укажите имя пользователя", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                if(User.Name.Length > 50 || User.Name.Length < 3)
                {
                    ModernMessageBox.Show("Некорректная длина имени пользователя", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                if (string.IsNullOrEmpty(User.Email))
                {
                    ModernMessageBox.Show("Укажите Email пользователя", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                if(!Regex.IsMatch(User.Email, emailRegex))
                {
                    ModernMessageBox.Show("Некорректный формат Email", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                if (TimeSpan.TryParse(RegisterTime, out TimeSpan time))
                {
                    RegisterDateSelected = RegisterDateSelected.Date + time;
                }
                else
                {
                    ModernMessageBox.Show("Неверный формат времени. Используйте ЧЧ:ММ (например: 14:30)",
                        "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (IsEditMode) //Если страница открыта в режиме редактирования
                {
                    var updateUserRequest = new PutUserRequest
                    {
                        Name = User.Name,
                        Email = User.Email,
                        CanUpload = User.CanUpload,
                        IsActive = User.IsActive,
                        RegisteredAt = RegisterDateSelected,
                    };
                    var response = await _apiService.UpdateAsync(User.Id, updateUserRequest);
                    if (response.Success)
                    {
                        ModernMessageBox.Show($"Данные пользователя обновлены\nИзмененные таблицы:\n" +
                            $" {DisplayChangesHelper.GetAffectedTablesString(response.AffectedTables)}",
                            "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                        GoBack();
                    }
                    else
                    {
                        ModernMessageBox.Show($"Непредвиденная ошибка: {response.Message}\nИзмененные таблицы:\n" +
                            $" {DisplayChangesHelper.GetAffectedTablesString(response.AffectedTables)}",
                            "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                else
                {
                    if (string.IsNullOrEmpty(Password))
                    {
                        ModernMessageBox.Show("Укажите пароль пользователя", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                    if (Password.Length < AppConfig.PASSWORD_MIN_LENGTH || Password.Length > AppConfig.PASSWORD_MAX_LENGTH)
                    {
                        ModernMessageBox.Show($"Некорректная длина пароля (от {AppConfig.PASSWORD_MIN_LENGTH} до {AppConfig.PASSWORD_MAX_LENGTH} символов)", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                    var newUserRequest = new PostUserRequest
                    {
                        UserName = User.Name,
                        Email = User.Email,
                        Password = Password, //На сервере будет захеширован
                        CanUpload = User.CanUpload,
                        IsActive = User.IsActive,
                    };
                    var response = await _apiService.AddAsync(newUserRequest);
                    if (response.Success)
                    {
                        ModernMessageBox.Show($"Пользователь успешно создан\nИзмененные таблицы:\n" +
                            $" {DisplayChangesHelper.GetAffectedTablesString(response.AffectedTables)}",
                            "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                        GoBack();
                    }
                    else
                    {
                        ModernMessageBox.Show($"Непредвиденная ошибка: {response.Message}\nИзмененные таблицы:\n" +
                            $" {DisplayChangesHelper.GetAffectedTablesString(response.AffectedTables)}",
                            "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                    Password = string.Empty;
                }
            }
            catch (Exception ex)
            {
                ModernMessageBox.Show($"Внутренняя ошибка программы: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task ResetPassword()
        {
            try
            {
                IsBusy = true;

                string? newPassword = ResetPasswordWindow.ShowResetPasswordDialog(User.Id.ToString(), User.Name, User.Email);
                if (newPassword != null)
                {
                    var response = await _apiService.ResetPasswordAsync(User.Id, newPassword);
                    if (response.Success)
                    {
                        ModernMessageBox.Show($"Пароль успешно сброшен и установлен новый\nИзмененные таблицы:\n" +
                            $" {DisplayChangesHelper.GetAffectedTablesString(response.AffectedTables)}",
                            "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        ModernMessageBox.Show($"Непредвиденная ошибка: {response.Message}\nИзмененные таблицы:\n" +
                            $" {DisplayChangesHelper.GetAffectedTablesString(response.AffectedTables)}",
                            "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                ModernMessageBox.Show($"Внутренняя ошибка программы: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task DeleteUser()
        {
            try
            {
                IsBusy = true;
                var result = ModernMessageBox.Show($"Вы уверены, что хотите удалить пользователя {User.Name}?\nЭто действие невозможно отменить.", "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result == MessageBoxResult.Yes)
                {
                    var response = await _apiService.DeleteAsync<User>(User.Id);
                    if (response.Success)
                    {
                        ModernMessageBox.Show($"Пользователь успешно удален\nИзмененные таблицы:\n" +
                            $" {DisplayChangesHelper.GetAffectedTablesString(response.AffectedTables)}",
                            "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                        GoBack();
                    }
                    else
                    {
                        ModernMessageBox.Show($"Непредвиденная ошибка: {response.Message}\nИзмененные таблицы:\n" +
                            $" {DisplayChangesHelper.GetAffectedTablesString(response.AffectedTables)}",
                            "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                ModernMessageBox.Show($"Внутренняя ошибка программы: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private void GoBack()
        {
            OnGoBackRequested?.Invoke();
        }

        #endregion

        #region Навигация

        public event Action? OnGoBackRequested;

        #endregion

        #region Конструктор

        public EditUserViewModel(ApiService apiService, GetUserResponse? user = null)
        {
            _apiService = apiService;
            if (user != null)
            {
                User = user;
                IsEditMode = true;
                Title = $"Редактирование пользователя: {User.Name}";
            }
            else
            {
                User = new GetUserResponse();
                IsEditMode = false;
                Title = "Создание нового пользователя";
            }
        }

        #endregion
    }
}
