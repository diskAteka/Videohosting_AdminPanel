using SharedLib.Contracts.Response.Video;
using SharedLib.Contracts.RezultModels;

namespace VideohostingPanel.Interfaces
{
    public interface IApiService
    {
        public Task<List<T>> GetListAsync<T>(int page = 1, int pageSize = 20, string? sortBy = null, bool sortAscending = true);

        public Task<T> GetByIdAsync<T>(int id);

        public Task<AddRezult> AddAsync<T>(T dto);

        public Task<UpdateRezult> UpdateAsync<T>(int id, T dto);

        public Task<DeleteResult> DeleteAsync<T>(int id);

        public Task<int> GetCountAsync(string type, bool isAdmin = true);

        public Task<List<UserVideoListItemDto>> GetUserVideosAsync(int userId);
    }
}
