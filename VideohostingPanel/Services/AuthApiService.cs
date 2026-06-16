using System.Net.Http;

namespace VideohostingPanel.Services
{
    /// <summary>
    /// Управляет базовым адресом HttpClient и токеном авторизации
    /// </summary>
    public class AuthApiService
    {
        private readonly HttpClient _httpClient;
        private readonly AuthTokenHandler _tokenHandler;

        public const string WPF_PREFIX = "/wpf";
        public const string API_PREFIX = "/api";
        public const string AUTH_PATH = "/api/auth/login";

        public AuthApiService(HttpClient httpClient, AuthTokenHandler tokenHandler)
        {
            _httpClient = httpClient;
            _tokenHandler = tokenHandler;
        }

        public void SetBaseUrl(string serverAddress, string port)
        {
            var baseUrl = $"https://{serverAddress}:{port}";
            _httpClient.BaseAddress = new Uri(baseUrl);
        }

        public void SetToken(string? token) => _tokenHandler.SetToken(token);

        public bool HasToken => _tokenHandler.HasToken;

        public void ClearAuth() => _tokenHandler.SetToken(null);
    }
}