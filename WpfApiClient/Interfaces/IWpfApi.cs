using SharedLib.Contracts.Admin;
using SharedLib.Contracts.Response.Video;
using SharedLib.Contracts.RezultModels;

namespace WpfApiClient.Interfaces
{
    public interface IWpfApi
    {
        // Списки (GET)
        Task<List<T>> GetListAsync<T>(int page = 1, int pageSize = 20, string? sortBy = null, bool sortAscending = true);
        Task<List<UserVideoListItemDto>> GetUserVideosAsync(int userId);

        // Получение по ID (GET)
        Task<T> GetByIdAsync<T>(int id);

        // Добавление (POST)
        Task<AddRezult> AddAsync<T>(T dto);

        // Обновление (PUT)
        Task<UpdateRezult> UpdateAsync<T>(int id, T dto);

        // Удаление (DELETE)
        Task<DeleteResult> DeleteAsync<T>(int id);

        // Сброс пароля пользователя
        Task<UpdateRezult> ResetPasswordAsync(int userId, ForcedPasswordResetRequest request);

        // Статистика
        Task<int> GetCountAsync(string type, bool isAdmin = true);
    }
}
