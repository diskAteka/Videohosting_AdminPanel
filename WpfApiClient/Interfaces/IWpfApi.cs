using SharedLib.Contracts.Admin;
using SharedLib.Contracts.Response.Video;
using SharedLib.Contracts.RezultModels;

namespace WpfApiClient.Interfaces
{
    public interface IWpfApi
    {
        Task<List<T>> GetListAsync<T>(int page = 1, int pageSize = 20, string? sortBy = null, bool sortAscending = true);

        Task<List<UserVideoListItemDto>> GetUserVideosAsync( int userId);

        Task<T> GetByIdAsync<T>(int id);

        Task<AddRezult> AddAsync<T>(T dto);

        Task<UpdateRezult> UpdateAsync<T>(int id, T dto);

        Task<DeleteResult> DeleteAsync<T>(int id);

        Task<int> GetCountAsync(string type, bool isAdmin = true);
    }
}
