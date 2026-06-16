using System.Net.Http;
using WpfApiClient.Config;

namespace VideohostingPanel.Services
{
    /// <summary>
    /// Управляет базовым адресом HttpClient и токеном авторизации
    /// </summary>
    public class AuthApiService
    {
        private readonly HttpClient _httpClient;
        private readonly AuthTokenHandler _tokenHandler;

        public AuthApiService(HttpClient httpClient, AuthTokenHandler tokenHandler)
        {
            _httpClient = httpClient;
            _tokenHandler = tokenHandler;
        }

        public void SetBaseUrl(string serverAddress, string port)
        {
            var baseUrl = AppConfig.BuildBaseUrl(serverAddress, port);
            _httpClient.BaseAddress = new Uri(baseUrl);
        }

        public void SetToken(string? token) => _tokenHandler.SetToken(token);

        public bool HasToken => _tokenHandler.HasToken;

        public void ClearAuth() => _tokenHandler.SetToken(null);
    }
}