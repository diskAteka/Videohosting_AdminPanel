using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using SharedLib.Contracts.Admin;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using VideohostingPanel.Resources.Elements;
using VideohostingPanel.Services;
using VideohostingPanel.ViewModels.Base;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;

namespace VideohostingPanel.ViewModels.AddViewModels
{
    public partial class AddVideoViewModel : BaseViewModel
    {
        private readonly ApiService _apiService;

        #region Свойства для макета

        [ObservableProperty]
        private PostVideoRequest _video;

        [ObservableProperty]
        private string? _posterFileName;

        [ObservableProperty]
        private string? _posterFilePath;

        [ObservableProperty]
        private bool _posterIsSelected;

        [ObservableProperty]
        private string? _videoFileName;

        [ObservableProperty]
        private string? _videoFilePath;

        [ObservableProperty]
        private bool _videoIsSelected;

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

        [ObservableProperty]
        private DateTime _selectedUploadDate = DateTime.UtcNow;

        [ObservableProperty]
        private string _uploadTime = DateTime.Now.ToString("HH:mm");

        #endregion

        #region Команды для файлов

        [RelayCommand]
        private void BrowsePosterFile()
        {
            var openFileDialog = new OpenFileDialog
            {
                Title = "Выберите постер видео",
                Filter = "Изображения (*.jpg;*.jpeg;*.png)|*.jpg;*.jpeg;*.png|Все файлы (*.*)|*.*",
                Multiselect = false
            };

            if (openFileDialog.ShowDialog() == true)
            {
                PosterFilePath = openFileDialog.FileName;
                PosterFileName = Path.GetFileName(openFileDialog.FileName);
                PosterIsSelected = true;
            }
        }

        [RelayCommand]
        private void BrowseVideoFile()
        {
            var openFileDialog = new OpenFileDialog
            {
                Title = "Выберите видеофайл",
                Filter = "Видео (*.mp4)|*.mp4;*.avi;*.mkv;*.webm|Все файлы (*.*)|*.*",
                Multiselect = false
            };

            if (openFileDialog.ShowDialog() == true)
            {
                var fileInfo = new FileInfo(openFileDialog.FileName);

                if (fileInfo.Length > 2L * 1024 * 1024 * 1024)
                {
                    ModernMessageBox.Show(
                        "Размер файла превышает 2 ГБ. Пожалуйста, выберите файл меньшего размера.",
                        "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                VideoFilePath = openFileDialog.FileName;
                VideoFileName = Path.GetFileName(openFileDialog.FileName);
                VideoIsSelected = true;
            }
        }

        #endregion

        #region Команды для данных

        [RelayCommand]
        private async Task CreateVideo()
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

                if (!VideoIsSelected)
                {
                    ModernMessageBox.Show("Выберите видеофайл", "Предупреждение",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (SelectedAuthor == null)
                {
                    ModernMessageBox.Show("Выберите автора видео", "Предупреждение",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                if (TimeSpan.TryParse(UploadTime, out TimeSpan time))
                {
                    SelectedUploadDate = SelectedUploadDate.Date + time;
                }
                else
                {
                    ModernMessageBox.Show("Неверный формат времени. Используйте ЧЧ:ММ (например: 14:30)",
                        "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                Video.AuthorId = SelectedAuthor.Id;
                Video.IsVerified = SelectedVerificationStatus.IsVerified;
                Video.DateUpload = SelectedUploadDate;

                if (!PosterIsSelected)
                {
                    Video.Poster = "/posters/default.jpg";
                }
                else
                {
                    Video.Poster = PosterFilePath!;
                }

                Video.Link = VideoFilePath!;

                var response = await _apiService.AddAsync(Video);

                if (response.Success)
                {
                    ModernMessageBox.Show(
                        $"Видео успешно создано\nИзмененные таблицы:\n" +
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
        private async Task SearchAuthorById()
        {
            try
            {
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

        public AddVideoViewModel(ApiService apiService)
        {
            _apiService = apiService;
            Title = "Добавление нового видео";

            Video = new PostVideoRequest
            {
                Name = string.Empty,
                Description = null,
                DateUpload = DateTime.Now,
                Link = string.Empty,
                Poster = string.Empty,
                Likes = 0,
                Dislikes = 0,
                IsVerified = false,
                Views = 0,
                AuthorId = 0
            };

            SelectedVerificationStatus = VerificationStatuses[0]; // "Не верифицировано"
            SelectedUploadDate = DateTime.Now;

            _ = LoadAuthorsCommand.ExecuteAsync(null);
        }

        #endregion

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
}