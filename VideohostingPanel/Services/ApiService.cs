using SharedLib.Contracts.Response.Video;
using SharedLib.Contracts.RezultModels;
using VideohostingPanel.Interfaces;
using WpfApiClient.Interfaces;

namespace VideohostingPanel.Services
{
    public class ApiService : IApiService , IService
    {
        private readonly IWpfApi _apiClient;

        public ApiService(IWpfApi apiClient)
        {
            _apiClient = apiClient;
        }

        public async Task<List<T>> GetListAsync<T>(int page = 1, int pageSize = 20, string? sortBy = null, bool sortAscending = true)
        {
            return await _apiClient.GetListAsync<T>(page, pageSize, sortBy, sortAscending);
        }// Получение списка

        public async Task<T> GetByIdAsync<T>(int id)
        {
            return await _apiClient.GetByIdAsync<T>(id);
        }// Получение по ID

        public async Task<AddRezult> AddAsync<T>(T dto)
        {
            return await _apiClient.AddAsync(dto);
        }// Добавление

        public async Task<UpdateRezult> UpdateAsync<T>(int id, T dto)
        {
            return await _apiClient.UpdateAsync(id, dto);
        }// Обновление
        
        public async Task<DeleteResult> DeleteAsync<T>(int id)
        {
            return await _apiClient.DeleteAsync<T>(id);
        }// Удаление
        
        public async Task<int> GetCountAsync(string type, bool isAdmin = true)
        {
            return await _apiClient.GetCountAsync(type, isAdmin);
        }// Получение количества записей
       
        public async Task<List<UserVideoListItemDto>> GetUserVideosAsync(int userId)
        {
            return await _apiClient.GetUserVideosAsync(userId);
        }// Получение видео пользователя
    }
}
