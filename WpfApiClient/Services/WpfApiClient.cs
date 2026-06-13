using SharedLib.Contracts.Admin;
using SharedLib.Contracts.Response.Video;
using SharedLib.Contracts.RezultModels;
using SharedLib.Models;
using System.Net.Http.Json;
using System.Text.Json;
using WpfApiClient.Interfaces;
using System.Net.Http.Headers;

namespace WpfApiClient.Services
{
    public class WpfApiClient : IWpfApi
    {
        private readonly HttpClient _httpClient;
        private readonly JsonSerializerOptions _jsonOptions;
        private readonly string baseApiRoute = "/wpf";

        private static readonly Dictionary<Type, string> EntityEndpoints = new()
        {
            // Для GET списков и GetById - используются Response/DTO классы
            { typeof(GetVideoResponse), "video" },
            { typeof(GetUserResponse), "user" },
            { typeof(GetCommentResponse), "comment" },
            { typeof(GetLikeResponse), "like" },
            { typeof(GetDislikeResponse), "dislike" },
            { typeof(GetViewResponse), "view" },
            { typeof(GetServerLogResponse), "log" },
            { typeof(Employee), "employee" },
            
            // Для POST/PUT - используются Request классы
            { typeof(PostVideoRequest), "video" },
            { typeof(PostUserRequest), "user" },
            { typeof(PutUserRequest), "user" },
            { typeof(PostPutCommentRequest), "comment" },
            { typeof(PostLikeRequest), "like" },
            { typeof(PostDislikeRequest), "dislike" },
            { typeof(PostViewRequest), "view" },
            { typeof(PostPutEmployeeRequest), "employee" }
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
            return result ?? new List<T>();
        }

        public async Task<List<UserVideoListItemDto>> GetUserVideosAsync(int userId)
        {
            var url = $"{baseApiRoute}/get/user/videos?userId={userId}";
            var response = await _httpClient.GetAsync(url);

            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<List<UserVideoListItemDto>>(_jsonOptions);
            return result ?? new List<UserVideoListItemDto>();
        }

        public async Task<T> GetByIdAsync<T>(int id)
        {
            var entity = GetEntityEndpoint<T>();
            var response = await _httpClient.GetAsync($"{baseApiRoute}/get/{entity}/{id}");

            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<T>(_jsonOptions)
                ?? throw new InvalidOperationException($"Не удалось десериализовать {typeof(T).Name}");
        }

        public async Task<AddRezult> AddAsync<T>(T dto)
        {
            var entity = GetEntityEndpoint<T>();
            var response = await _httpClient.PostAsJsonAsync(
                $"{baseApiRoute}/add/{entity}", dto, _jsonOptions);

            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<AddRezult>(_jsonOptions);
            if (!result.Success)
                throw new InvalidOperationException($"Не удалось добавить {typeof(T).Name}: {result.Message}");
            return result;
        }

        public async Task<UpdateRezult> UpdateAsync<T>(int id, T dto)
        {
            var entity = GetEntityEndpoint<T>();
            var response = await _httpClient.PutAsJsonAsync(
                $"{baseApiRoute}/update/{entity}/{id}", dto, _jsonOptions);

            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<UpdateRezult>(_jsonOptions);
            if (!result.Success)
                throw new InvalidOperationException($"Не удалось обновить {typeof(T).Name}: {result.Message}");
            return result;
        }

        public async Task<DeleteResult> DeleteAsync<T>(int id)
        {
            var entity = GetEntityEndpoint<T>();
            var response = await _httpClient.DeleteAsync($"{baseApiRoute}/delete/{entity}/{id}");

            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<DeleteResult>(_jsonOptions);
            if (!result.Success)
                throw new InvalidOperationException($"Не удалось удалить {typeof(T).Name}: {result.Message}");
            return result;
        }

        public async Task<UpdateRezult> ResetPasswordAsync(int userId, ForcedPasswordResetRequest request)
        {
            var response = await _httpClient.PutAsJsonAsync(
                $"{baseApiRoute}/reset/password/{userId}", request, _jsonOptions);

            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<UpdateRezult>(_jsonOptions);
            if (!result.Success)
                throw new InvalidOperationException($"Не удалось сбросить пароль: {result.Message}");
            return result;
        }

        public async Task<int> GetCountAsync(string type, bool isAdmin = true)
        {
            var rolePath = isAdmin ? "admin" : "virifier";
            var url = $"{baseApiRoute}/{rolePath}/get/count?type={Uri.EscapeDataString(type)}";
            var response = await _httpClient.GetAsync(url);

            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<int>(_jsonOptions);
        }

        public async Task<UpdateRezult> VerifyVideoAsync(int videoId)
        {
            var response = await _httpClient.PutAsync($"{baseApiRoute}/video/verify/{videoId}", null);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<UpdateRezult>(_jsonOptions);
            if (!result.Success)
                throw new InvalidOperationException($"Не удалось верифицировать видео: {result.Message}");
            return result;
        }

        public async Task<Stream> StreamVideoAsync(int videoId, long? startByte)
        {
            startByte = startByte ?? 0;
            var request = new HttpRequestMessage(HttpMethod.Get, $"{baseApiRoute}/stream/{videoId}");
            request.Headers.Range = new RangeHeaderValue(startByte, null);
            var response = await _httpClient.GetAsync($"{baseApiRoute}/stream/{videoId}",
                HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStreamAsync();
        }

        public async Task<long> GetLengthOfStream(int videoId)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"{baseApiRoute}/stream/{videoId}");
            var respose = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            return respose.Content.Headers.ContentLength ?? 0;
        }

        public async Task<Stream> GetPosterAsync(int videoId)
        {
            var response = await _httpClient.GetAsync($"{baseApiRoute}/video/poster/{videoId}",
                HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStreamAsync();
        }
    }
}
