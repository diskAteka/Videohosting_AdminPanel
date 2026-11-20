using SharedLib.Contracts.Admin;
using SharedLib.Contracts.Response.Video;
using SharedLib.Contracts.RezultModels;
using SharedLib.Models;
using System.Net.Http.Json;
using System.Text.Json;
using WpfApiClient.Interfaces;
using System.Net.Http.Headers;
using WpfApiClient.Config;

namespace WpfApiClient.Services
{
    public class WpfApiClient : IWpfApi
    {
        private readonly HttpClient _httpClient;
        private readonly JsonSerializerOptions _jsonOptions;
        private string _apiBaseUrl = AppConfig.WPF_PREFIX;


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

        public void SetBaseUrl(string server, string port)
        {
            // Не меняем HttpClient.BaseAddress: после первого запроса это бросает
            // InvalidOperationException. Вместо этого храним абсолютный префикс —
            // он перекрывает BaseAddress и позволяет повторный вход.
            _apiBaseUrl = $"{AppConfig.BuildBaseUrl(server, port)}{AppConfig.WPF_PREFIX}";
        }

        private string GetEntityEndpoint<T>()
        {
            if (EntityEndpoints.TryGetValue(typeof(T), out var endpoint))
                return endpoint;

            throw new ArgumentException(
                $"Неизвестный тип сущности: {typeof(T).Name}. " +
                $"Зарегистрируйте его в {nameof(EntityEndpoints)}.");
        }

        public async Task<List<T>> GetListAsync<T>(int page = 1, int pageSize = 20, string? sortBy = null, bool sortAscending = true,
            string? searchName = null, string? searchText = null, string? actionType = null)
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

            // Фильтры поиска по имени / тексту / типу действия; пустые отключают фильтр
            if (!string.IsNullOrWhiteSpace(searchName))
                queryParams.Add($"SearchName={Uri.EscapeDataString(searchName)}");
            if (!string.IsNullOrWhiteSpace(searchText))
                queryParams.Add($"SearchText={Uri.EscapeDataString(searchText)}");
            if (!string.IsNullOrWhiteSpace(actionType))
                queryParams.Add($"ActionType={Uri.EscapeDataString(actionType)}");

            var url = $"{_apiBaseUrl}/get/{entity}s?{string.Join("&", queryParams)}";
            var response = await _httpClient.GetAsync(url);

            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<List<T>>(_jsonOptions);
            return result ?? new List<T>();
        }

        public async Task<List<UserVideoListItemDto>> GetUserVideosAsync(int userId)
        {
            var url = $"{_apiBaseUrl}/get/user/videos?userId={userId}";
            var response = await _httpClient.GetAsync(url);

            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<List<UserVideoListItemDto>>(_jsonOptions);
            return result ?? new List<UserVideoListItemDto>();
        }

        public async Task<List<GetVideoResponse>> GetUnverifiedVideosAsync(int page = 1, int pageSize = 20)
        {
            var url = $"{_apiBaseUrl}/virifier/videos?Page={page}&PageSize={pageSize}";
            var response = await _httpClient.GetAsync(url);

            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<List<GetVideoResponse>>(_jsonOptions);
            return result ?? new List<GetVideoResponse>();
        }

        public async Task<T> GetByIdAsync<T>(int id)
        {
            var entity = GetEntityEndpoint<T>();
            var response = await _httpClient.GetAsync($"{_apiBaseUrl}/get/{entity}/{id}");

            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<T>(_jsonOptions)
                ?? throw new InvalidOperationException($"Не удалось десериализовать {typeof(T).Name}");
        }

        public async Task<AddRezult> AddAsync<T>(T dto)
        {
            var entity = GetEntityEndpoint<T>();
            var response = await _httpClient.PostAsJsonAsync(
                $"{_apiBaseUrl}/add/{entity}", dto, _jsonOptions);

            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<AddRezult>(_jsonOptions);
            if (!result.Success)
                throw new InvalidOperationException($"Не удалось добавить {typeof(T).Name}: {result.Message}");
            return result;
        }

        public async Task<AddRezult> LogActionAsync(ServerLog log, CancellationToken ct = default)
        {
            var response = await _httpClient.PostAsJsonAsync($"{_apiBaseUrl}/log", log, _jsonOptions, ct);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<AddRezult>(_jsonOptions) ?? new AddRezult(null, false);
        }

        public async Task<UpdateRezult> UpdateAsync<T>(int id, T dto)
        {
            var entity = GetEntityEndpoint<T>();
            var response = await _httpClient.PutAsJsonAsync(
                $"{_apiBaseUrl}/update/{entity}/{id}", dto, _jsonOptions);

            response.EnsureSuccessStatusCode();
            return await ReadUpdateRezultAsync(response);
        }

        public async Task<DeleteResult> DeleteAsync<T>(int id)
        {
            var entity = GetEntityEndpoint<T>();
            var response = await _httpClient.DeleteAsync($"{_apiBaseUrl}/delete/{entity}/{id}");

            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<DeleteResult>(_jsonOptions);
            if (result == null || !result.Success)
                throw new InvalidOperationException($"Не удалось удалить {typeof(T).Name}: {result?.Message}");
            return result;
        }

        public async Task<UpdateRezult> ResetPasswordAsync(int userId, ForcedPasswordResetRequest request)
        {
            var response = await _httpClient.PutAsJsonAsync(
                $"{_apiBaseUrl}/reset/password/{userId}", request, _jsonOptions);

            response.EnsureSuccessStatusCode();
            return await ReadUpdateRezultAsync(response);
        }

        private async Task<UpdateRezult> ReadUpdateRezultAsync(HttpResponseMessage response)
        {
            var dto = await response.Content.ReadFromJsonAsync<UpdateRezultDto>(_jsonOptions);
            return new UpdateRezult(dto?.AffectedTables, dto?.Success ?? true)
            {
                Message = dto?.Message ?? "Успешное обновление записи"
            };
        }

        public async Task<int> GetCountAsync(string type, bool isAdmin = true,
            string? searchName = null, string? searchText = null, string? actionType = null)
        {
            var rolePath = isAdmin ? "admin" : "virifier";
            var queryParams = new List<string>
            {
                $"type={Uri.EscapeDataString(type)}"
            };

            // Счётчик должен считать по тем же фильтрам, что и список
            if (!string.IsNullOrWhiteSpace(searchName))
                queryParams.Add($"SearchName={Uri.EscapeDataString(searchName)}");
            if (!string.IsNullOrWhiteSpace(searchText))
                queryParams.Add($"SearchText={Uri.EscapeDataString(searchText)}");
            if (!string.IsNullOrWhiteSpace(actionType))
                queryParams.Add($"ActionType={Uri.EscapeDataString(actionType)}");

            var url = $"{_apiBaseUrl}/{rolePath}/get/count?{string.Join("&", queryParams)}";
            var response = await _httpClient.GetAsync(url);

            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<CountResponse>(_jsonOptions);
            return result?.Count ?? 0;
        }

        public async Task<UpdateRezult> VerifyVideoAsync(int videoId)
        {
            var response = await _httpClient.PutAsync($"{_apiBaseUrl}/video/verify/{videoId}", null);
            response.EnsureSuccessStatusCode();
            return await ReadUpdateRezultAsync(response);
        }

        public async Task<string> GetStreamUrlAsync(int videoId)
        {
            var response = await _httpClient.GetAsync($"{_apiBaseUrl}/stream/{videoId}");
            response.EnsureSuccessStatusCode();

            // Сервер отдаёт URL как text/plain (иногда в кавычках) — не как JSON-строку.
            var url = (await response.Content.ReadAsStringAsync()).Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(url))
                throw new InvalidOperationException("Сервер не вернул URL видеопотока");

            return url;
        }

        public async Task<Stream> GetPosterAsync(int videoId)
        {
            var response = await _httpClient.GetAsync($"{_apiBaseUrl}/video/poster/{videoId}",
                HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStreamAsync();
        }

        private sealed class UpdateRezultDto
        {
            public bool Success { get; set; }
            public string? Message { get; set; }
            public List<string?>? AffectedTables { get; set; }
        }

        private sealed class CountResponse
        {
            public int Count { get; set; }
        }
    }
}
