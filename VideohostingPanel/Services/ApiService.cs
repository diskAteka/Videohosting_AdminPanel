using SharedLib.Contracts.Admin;
using SharedLib.Contracts.Response.Video;
using SharedLib.Contracts.RezultModels;
using System.IO;
using WpfApiClient.Interfaces;

namespace VideohostingPanel.Services
{
    public class ApiService
    {
        private readonly IWpfApi _apiClient;

        public ApiService(IWpfApi apiClient)
        {
            _apiClient = apiClient;
        }

        // Применение адреса и порта сервера ко всем последующим запросам
        public void SetBaseUrl(string server, string port)
        {
            _apiClient.SetBaseUrl(server, port);
        }

        // Получение списка
        public async Task<List<T>> GetListAsync<T>(int page = 1, int pageSize = 20, string? sortBy = null, bool sortAscending = true)
        {
            return await _apiClient.GetListAsync<T>(page, pageSize, sortBy, sortAscending);
        }

        // Получение по ID
        public async Task<T> GetByIdAsync<T>(int id)
        {
            return await _apiClient.GetByIdAsync<T>(id);
        }

        // Добавление
        public async Task<AddRezult> AddAsync<T>(T dto)
        {
            return await _apiClient.AddAsync(dto);
        }

        // Обновление
        public async Task<UpdateRezult> UpdateAsync<T>(int id, T dto)
        {
            return await _apiClient.UpdateAsync(id, dto);
        }

        // Удаление
        public async Task<DeleteResult> DeleteAsync<T>(int id)
        {
            return await _apiClient.DeleteAsync<T>(id);
        }

        // Сброс пароля пользователя
        public async Task<UpdateRezult> ResetPasswordAsync(int userId, string newPassword)
        {
            var request = new ForcedPasswordResetRequest { NewPassword = newPassword };
            return await _apiClient.ResetPasswordAsync(userId, request);
        }

        // Получение количества записей
        public async Task<int> GetCountAsync(string type, bool isAdmin = true)
        {
            return await _apiClient.GetCountAsync(type, isAdmin);
        }

        // Получение видео пользователя
        public async Task<List<UserVideoListItemDto>> GetUserVideosAsync(int userId)
        {
            return await _apiClient.GetUserVideosAsync(userId);
        }

        // Получение списка неверифицированных видео (для верификатора)
        public async Task<List<GetVideoResponse>> GetUnverifiedVideosAsync(int page = 1, int pageSize = 20)
        {
            return await _apiClient.GetUnverifiedVideosAsync(page, pageSize);
        }

        public async Task<UpdateRezult> VerifyVideoAsync(int videoId)// Верификация видео
        {
            return await _apiClient.VerifyVideoAsync(videoId);
        }

        public async Task<string> GetStreamUrlAsync(int videoId)// URL видеопотока из хранилища
        {
            return await _apiClient.GetStreamUrlAsync(videoId);
        }

        public async Task<Stream> GetPosterAsync(int videoId)// Получение постера видео
        {
            return await _apiClient.GetPosterAsync(videoId);
        }
    }
}
