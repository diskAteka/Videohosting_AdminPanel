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
    public partial class EditCommentViewModel : BaseViewModel
    {
        private readonly ApiService _apiService;
        private readonly GetCommentResponse? _originalComment;

        #region Свойства для формы
        [ObservableProperty]
        private bool _isEditMode;

        [ObservableProperty]
        private GetCommentResponse _comment;

        [ObservableProperty]
        private ObservableCollection<ComboBoxItem> _videos = new();

        [ObservableProperty]
        private ComboBoxItem? _selectedVideo;

        [ObservableProperty]
        private string _videoSearchId = string.Empty;

        [ObservableProperty]
        private ObservableCollection<ComboBoxItem> _users = new();

        [ObservableProperty]
        private ComboBoxItem? _selectedUser;

        [ObservableProperty]
        private string _userSearchId = string.Empty;

        [ObservableProperty]
        private DateTime? _selectedUploadDate;

        [ObservableProperty]
        private string _uploadTime = DateTime.Now.ToString("HH:mm");

        #endregion

        #region Команды

        [RelayCommand]
        private async Task SaveAsync()
        {
            try
            {
                IsBusy = true; 
                if (SelectedVideo == null)
                {
                    ModernMessageBox.Show("Выберите видео", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                if (SelectedUser == null)
                {
                    ModernMessageBox.Show("Выберите пользователя", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                if (string.IsNullOrWhiteSpace(Comment.Text))
                {
                    ModernMessageBox.Show("Введите текст комментария", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                Comment.VideoId = SelectedVideo.Id;
                Comment.UserId = SelectedUser.Id;

                if (SelectedUploadDate == null)
                {
                    ModernMessageBox.Show("Укажите дату комментария", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                if (!TimeFormatHelper.TryParseHHmm(UploadTime, out TimeSpan time))
                {
                    ModernMessageBox.Show("Неверный формат времени. Используйте ЧЧ:ММ (например: 14:30)",
                        "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                var commentDate = SelectedUploadDate.Value.Date + time;

                if (IsEditMode)//Если включен режим редактирования
                {
                    var postDto = new PostPutCommentRequest
                    {
                        VideoId = Comment.VideoId,
                        UserId = Comment.UserId,
                        Text = Comment.Text,
                        Date = commentDate
                    };//Сформировать запрос на обновление

                    var response = await _apiService.UpdateAsync(Comment.Id, postDto);//Отправить запрос

                    if (response.Success)
                    {
                        ModernMessageBox.Show($"Данные комментария обновлены\nИзмененные таблицы:\n" +
                            $" {DisplayChangesHelper.GetAffectedTablesString(response.AffectedTables)}",
                            "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                        _ = _apiService.LogAsync(AuditTable.Comment, AuditAction.Update, $"Изменён комментарий id={Comment.Id}");
                        GoBack();
                    }
                    else
                    {
                        ModernMessageBox.Show($"Непредвиденная ошибка\nИзмененные таблицы:\n" +
                            $" {DisplayChangesHelper.GetAffectedTablesString(response.AffectedTables)}",
                            "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                else//Если включен режим создания нового объекта
                {
                    var postDto = new PostPutCommentRequest
                    {
                        VideoId = Comment.VideoId,
                        UserId = Comment.UserId,
                        Text = Comment.Text,
                        Date = commentDate
                    };//СФормировать запрос на создание

                    var response = await _apiService.AddAsync(postDto);//Отправить запрос

                    if (response.Success)
                    {
                        ModernMessageBox.Show($"Комментарий создан\nИзмененные таблицы:\n" +
                            $" {DisplayChangesHelper.GetAffectedTablesString(response.AffectedTables)}",
                            "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                        _ = _apiService.LogAsync(AuditTable.Comment, AuditAction.Insert, $"Создан комментарий к видео id={Comment.VideoId}");
                        GoBack();
                    }
                    else
                    {
                        ModernMessageBox.Show($"Ошибка сервера\nИзмененные таблицы:\n" +
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
        private void Cancel() => GoBack();

        [RelayCommand]
        private async Task DeleteAsync()
        {
            if (IsEditMode) return;

            var result = ModernMessageBox.Show(
                "Вы уверены? Это действие необратимо",
                "Подтверждение удаления",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                IsBusy = true;
                var deleteResult = await _apiService.DeleteAsync<GetCommentResponse>(Comment.Id);

                if (deleteResult.Success)
                {
                    ModernMessageBox.Show($"Комментарий удалён\nИзменено записей: {deleteResult.AffectedTables}", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                    _ = _apiService.LogAsync(AuditTable.Comment, AuditAction.Delete, $"Удалён комментарий id={Comment.Id}");
                    GoBack();
                }
                else
                {
                    ModernMessageBox.Show($"Непредвиденная ошибка\nИзменено записей: {deleteResult.AffectedTables}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                ModernMessageBox.Show($"Произошла ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task SearchVideoByIdAsync()
        {
            if (!int.TryParse(VideoSearchId, out int id))
            {
                ModernMessageBox.Show("Введите корректный ID видео", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                IsBusy = true;
                var video = await _apiService.GetByIdAsync<GetVideoResponse>(id);

                if (video != null)
                {
                    var item = Videos.FirstOrDefault(v => v.Id == video.Id);
                    if (item != null)
                    {
                        SelectedVideo = item;
                    }
                    else
                    {
                        ModernMessageBox.Show("Видео не найдено в списке", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                ModernMessageBox.Show($"Видео не найдено: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task SearchUserByIdAsync()
        {
            if (!int.TryParse(UserSearchId, out int id))
            {
                ModernMessageBox.Show("Введите корректный ID пользователя", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                IsBusy = true;
                var user = await _apiService.GetByIdAsync<GetUserResponse>(id);

                if (user != null)
                {
                    var item = Users.FirstOrDefault(u => u.Id == user.Id);
                    if (item != null)
                    {
                        SelectedUser = item;
                    }
                    else
                    {
                        ModernMessageBox.Show("Пользователь не найден в списке", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                ModernMessageBox.Show($"Пользователь не найден: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
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

        public EditCommentViewModel(ApiService apiService, GetCommentResponse? comment = null)
        {
            _apiService = apiService;
            _originalComment = comment;
            _isEditMode = comment != null;

            if (_isEditMode && comment != null)
            {
                _comment = new GetCommentResponse
                {
                    Id = comment.Id,
                    VideoId = comment.VideoId,
                    UserId = comment.UserId,
                    Text = comment.Text,
                    Date = comment.Date
                };
                _selectedUploadDate = comment.Date;
                _uploadTime = comment.Date.ToString("HH:mm");
                Title = $"Редактирование комментария";
            }
            else
            {
                _comment = new GetCommentResponse
                {
                    Date = DateTime.Now
                };
                _selectedUploadDate = DateTime.Now;
                Title = "Создание комментария";
            }

            _ = LoadDropdownDataAsync();
        }

        #endregion

        #region Методы загрузки данных

        private async Task LoadDropdownDataAsync()
        {
            try
            {
                IsBusy = true;
                await Task.WhenAll(LoadVideosAsync(), LoadUsersAsync());

                // Устанавливаем выбранные элементы для режима редактирования
                if (IsEditMode && _originalComment != null)
                {
                    SelectedVideo = Videos.FirstOrDefault(v => v.Id == _originalComment.VideoId);
                    SelectedUser = Users.FirstOrDefault(u => u.Id == _originalComment.UserId);
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task LoadVideosAsync()
        {
            try
            {
                var videos = await _apiService.GetListAsync<GetVideoResponse>(page: 1, pageSize: 1000);
                Videos.Clear();
                foreach (var video in videos)
                {
                    Videos.Add(new ComboBoxItem
                    {
                        Id = video.Id,
                        DisplayName = $"{video.Name} (ID: {video.Id})"
                    });
                }
            }
            catch (Exception)
            {
                // Список видео не критичен для редактирования комментария.
            }
        }

        private async Task LoadUsersAsync()
        {
            try
            {
                var users = await _apiService.GetListAsync<GetUserResponse>(page: 1, pageSize: 1000);
                Users.Clear();
                foreach (var user in users)
                {
                    Users.Add(new ComboBoxItem
                    {
                        Id = user.Id,
                        DisplayName = $"{user.Name} (ID: {user.Id})"
                    });
                }
            }
            catch (Exception)
            {
                // Список пользователей не критичен для редактирования комментария.
            }
        }

        #endregion
    }
}
