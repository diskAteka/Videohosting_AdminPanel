using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SharedLib.Contracts.Admin;
using System.Collections.ObjectModel;
using System.Windows;
using VideohostingPanel.Resources.Elements;
using VideohostingPanel.Services;
using VideohostingPanel.ViewModels.Base;

namespace VideohostingPanel.ViewModels.EditViewModels
{
    public partial class EditVideoViewModel : BaseViewModel
    {
        private readonly ApiService _apiService;

        #region Свйоства для разметки

        [ObservableProperty]
        private GetVideoResponse _video;

        [ObservableProperty]
        private bool _isEditMode = true;

        [ObservableProperty]
        private ObservableCollection<AuthorDisplayItem> _authors = new();

        [ObservableProperty]
        private AuthorDisplayItem? _selectedAuthor;

        [ObservableProperty]
        private string _authorSearchId = string.Empty;
        public ObservableCollection<VerificationStatusItem> VerificationStatuses { get; } = new()
        {
            new VerificationStatusItem(false, "Не верифицировано"),
            new VerificationStatusItem(true, "Верифицировано")
        };

        [ObservableProperty]
        private VerificationStatusItem _selectedVerificationStatus;

        #endregion

        #region Команды

        [RelayCommand]
        private async Task SaveChanges()
        {
            try
            {
                IsBusy = true;

                if (string.IsNullOrWhiteSpace(Video.Name))
                {
                    ModernMessageBox.Show("Укажите название видео", "Предупреждение",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (string.IsNullOrWhiteSpace(Video.Link))
                {
                    ModernMessageBox.Show("Укажите ссылку на видео", "Предупреждение",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (SelectedAuthor != null)
                    Video.AuthorId = SelectedAuthor.Id;

                if (SelectedVerificationStatus != null)
                    Video.IsVerified = SelectedVerificationStatus.IsVerified;

                var updateRequest = new PostVideoRequest
                {
                    Name = Video.Name,
                    Description = Video.Description,
                    DateUpload = Video.DateUpload,
                    Link = Video.Link,
                    Poster = Video.Poster,
                    Likes = Video.Likes,
                    Dislikes = Video.Dislikes,
                    IsVerified = Video.IsVerified,
                    Views = Video.Views,
                    AuthorId = Video.AuthorId
                };

                var response = await _apiService.UpdateAsync(Video.Id, updateRequest);

                if (response.Success)
                {
                    ModernMessageBox.Show(
                        $"Данные видео обновлены\nИзмененные таблицы:\n" +
                        $"{DisplayChangesHelper.GetAffectedTablesString(response.AffectedTables)}",
                        "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                    GoBack();
                }
                else
                {
                    ModernMessageBox.Show(
                        $"Непредвиденная ошибка: {response.Message}\nИзмененные таблицы:\n" +
                        $"{DisplayChangesHelper.GetAffectedTablesString(response.AffectedTables)}",
                        "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                ModernMessageBox.Show($"Внутренняя ошибка программы: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task DeleteVideo()
        {
            try
            {
                IsBusy = true;
                var result = ModernMessageBox.Show(
                    $"Вы уверены, что хотите удалить видео \"{Video.Name}\"?\nЭто действие невозможно отменить.",
                    "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    var response = await _apiService.DeleteAsync<GetVideoResponse>(Video.Id);
                    if (response.Success)
                    {
                        ModernMessageBox.Show(
                            $"Видео успешно удалено\nИзмененные таблицы:\n" +
                            $"{DisplayChangesHelper.GetAffectedTablesString(response.AffectedTables)}",
                            "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                        GoBack();
                    }
                    else
                    {
                        ModernMessageBox.Show(
                            $"Непредвиденная ошибка: {response.Message}\nИзмененные таблицы:\n" +
                            $"{DisplayChangesHelper.GetAffectedTablesString(response.AffectedTables)}",
                            "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                ModernMessageBox.Show($"Внутренняя ошибка программы: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task SearchAuthorById()
        {
            try
            {
                IsBusy = true;

                if (!int.TryParse(AuthorSearchId, out int authorId))
                {
                    ModernMessageBox.Show("Введите корректный ID автора (целое число)",
                        "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var user = await _apiService.GetByIdAsync<GetUserResponse>(authorId);
                if (user != null)
                {
                    var displayItem = new AuthorDisplayItem(user.Id, user.Name, user.Email);
                    if (!Authors.Any(a => a.Id == user.Id))
                        Authors.Add(displayItem);

                    SelectedAuthor = Authors.First(a => a.Id == user.Id);
                    AuthorSearchId = string.Empty;

                    ModernMessageBox.Show($"Автор найден: {user.Name}",
                        "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                ModernMessageBox.Show($"Автор с ID {AuthorSearchId} не найден\n{ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task LoadAuthors()
        {
            try
            {
                var users = await _apiService.GetListAsync<GetUserResponse>(pageSize: 1000);
                Authors.Clear();
                foreach (var user in users)
                {
                    Authors.Add(new AuthorDisplayItem(user.Id, user.Name, user.Email));
                }
            }
            catch (Exception ex)
            {
                ModernMessageBox.Show($"Не удалось загрузить список авторов: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
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

        public EditVideoViewModel(ApiService apiService, GetVideoResponse? video = null)
        {
            _apiService = apiService;

            if (video != null)
            {
                Video = video;
                IsEditMode = true;
                Title = $"Редактирование видео: {Video.Name}";
            }
            else
            {
                Video = new GetVideoResponse
                {
                    DateUpload = DateTime.Now,
                    IsVerified = false,
                    Link = string.Empty,
                    Poster = string.Empty,
                    Name = string.Empty
                };
                IsEditMode = false;
                Title = "Создание нового видео";
            }

            SelectedVerificationStatus = VerificationStatuses.First(s => s.IsVerified == Video.IsVerified);

            _ = LoadAuthorsCommand.ExecuteAsync(null); //Асинхронно загружает авторов при инициализации модели представления
        }

        #endregion
    }

    #region Вспомогательные классы

    public class AuthorDisplayItem
    {
        public int Id { get; }
        public string Name { get; }
        public string Email { get; }
        public string DisplayName => $"{Name} (ID: {Id})";

        public AuthorDisplayItem(int id, string name, string email)
        {
            Id = id;
            Name = name;
            Email = email;
        }
    }

    public class VerificationStatusItem
    {
        public bool IsVerified { get; }
        public string DisplayName { get; }

        public VerificationStatusItem(bool isVerified, string displayName)
        {
            IsVerified = isVerified;
            DisplayName = displayName;
        }
    }

    #endregion
}