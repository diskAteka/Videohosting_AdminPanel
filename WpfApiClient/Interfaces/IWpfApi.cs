using SharedLib.Contracts.Admin;
using SharedLib.Contracts.Response.Video;
using SharedLib.Contracts.RezultModels;

namespace WpfApiClient.Interfaces
{
    public interface IWpfApi
    {
        #region Конфигурация
        void SetBaseUrl(string server, string port);
        #endregion

        #region Списки (GET)
        Task<List<T>> GetListAsync<T>(int page = 1, int pageSize = 20, string? sortBy = null, bool sortAscending = true);
        Task<List<UserVideoListItemDto>> GetUserVideosAsync(int userId);
        Task<List<GetVideoResponse>> GetUnverifiedVideosAsync(int page = 1, int pageSize = 20);
        #endregion

        #region Получение по ID (GET)
        Task<T> GetByIdAsync<T>(int id);
        #endregion

        #region Добавление (POST)
        Task<AddRezult> AddAsync<T>(T dto);
        #endregion

        #region Обновление (PUT)
        Task<UpdateRezult> UpdateAsync<T>(int id, T dto);
        #endregion

        #region Удаление (DELETE)
        Task<DeleteResult> DeleteAsync<T>(int id);
        #endregion

        #region Сброс пароля пользователя
        Task<UpdateRezult> ResetPasswordAsync(int userId, ForcedPasswordResetRequest request);
        #endregion

        #region Статистика
        Task<int> GetCountAsync(string type, bool isAdmin = true);
        #endregion

        #region Верификация видео (PUT)
        Task<UpdateRezult> VerifyVideoAsync(int videoId);
        #endregion

        #region Потоковое видео (GET)
        Task<string> GetStreamUrlAsync(int videoId);
        #endregion

        #region Постер видео (GET)
        Task<Stream> GetPosterAsync(int videoId);
        #endregion
    }
}
