using SharedLib.Contracts.Admin;
using SharedLib.Contracts.Response.Video;
using SharedLib.Contracts.RezultModels;
using SharedLib.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using WpfApiClient.Interfaces;

namespace WpfApiClient.Services
{
    public class WpfApiClient : IWpfApi
    {
        private readonly HttpClient _httpClient;
        private readonly JsonSerializerOptions _jsonOptions;

        private static readonly Dictionary<Type, string> EntityEndpoints = new()
        {
            { typeof(Employee), "employee" },
            { typeof(VideoDto), "video" },
            { typeof(UserDto), "user" },
            { typeof(CommentDto), "comment" },
            { typeof(LikeDto), "like" },
            { typeof(DislikeDto), "dislike" },
            { typeof(ViewDto), "view" },
            { typeof(ServerLogDto), "log" }
        };

        public WpfApiClient(HttpClient httpClient)
        {
            _httpClient = httpClient;
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
        }

        private string GetEntityEndpoint<T>()
        {
            if (EntityEndpoints.TryGetValue(typeof(T), out var endpoint))
                return endpoint;

            throw new ArgumentException(
                $"Неизвестный тип сущности: {typeof(T).Name}. " +
                $"Зарегистрируйте его в {nameof(EntityEndpoints)}.");
        }


        public async Task<List<T>> GetListAsync<T>(int page = 1, int pageSize = 20, string? sortBy = null, bool sortAscending = true)
        {
            var entity = GetEntityEndpoint<T>();
            var queryParams = new List<string>
            {
                $"Page={page}",
                $"PageSize={pageSize}"
            };

            if (!string.IsNullOrWhiteSpace(sortBy))
                queryParams.Add($"SortBy={Uri.EscapeDataString(sortBy)}");
            queryParams.Add($"SortAscending={sortAscending.ToString().ToLower()}");

            var url = $"/wpf/get/{entity}s?{string.Join("&", queryParams)}";
            var response = await _httpClient.GetAsync(url);

            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<List<T>>(_jsonOptions);
            if(result == null)
                return new List<T>();
            return result;
        }

        public async Task<List<UserVideoListItemDto>> GetUserVideosAsync(int userId)
        {
            var url = $"/wpf/get/user/videos?userId={userId}";
            var response = await _httpClient.GetAsync(url);

            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<List<UserVideoListItemDto>>(_jsonOptions);
            if(result == null)
                return new List<UserVideoListItemDto>();
            return result;
        }


        public async Task<T> GetByIdAsync<T>(int id)
        {
            var entity = GetEntityEndpoint<T>();
            var response = await _httpClient.GetAsync($"/wpf/get/{entity}/{id}");

            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<T>(_jsonOptions)
                ?? throw new InvalidOperationException($"Не удалось десериализовать {typeof(T).Name}");
        }

        public async Task<AddRezult> AddAsync<T>(T dto)
        {
            var entity = GetEntityEndpoint<T>();
            var response = await _httpClient.PostAsJsonAsync(
                $"/wpf/add/{entity}", dto, _jsonOptions);

            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<AddRezult>(_jsonOptions);
            if(!result.Success)
                 throw new InvalidOperationException("Не удалось получить результат добавления.");
            return result;
        }

        public async Task<UpdateRezult> UpdateAsync<T>(int id, T dto)
        {
            var entity = GetEntityEndpoint<T>();
            var response = await _httpClient.PutAsJsonAsync(
                $"/wpf/update/{entity}/{id}", dto, _jsonOptions);

            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<UpdateRezult>(_jsonOptions);
            if(!result.Success)
                 throw new InvalidOperationException("Не удалось получить результат обновления.");
            return result;
        }

        public async Task<DeleteResult> DeleteAsync<T>(int id)
        {
            var entity = GetEntityEndpoint<T>();
            var response = await _httpClient.DeleteAsync($"/wpf/delete/{entity}/{id}");

            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<DeleteResult>(_jsonOptions);
            if(!result.Success)
                 throw new InvalidOperationException("Не удалось получить результат удаления.");
            return result;
        }

        public async Task<int> GetCountAsync(string type, bool isAdmin = true)
        {
            var rolePath = isAdmin ? "admin" : "virifier";
            var url = $"/wpf/{rolePath}/get/count?type={Uri.EscapeDataString(type)}";
            var response = await _httpClient.GetAsync(url);

            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<int>(_jsonOptions);
        }

    }
}
