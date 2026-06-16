using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WpfApiClient.Config
{
    /// <summary>
    /// Централизованная конфигурация всех чувствительных данных и путей API.
    /// Изменяйте значения здесь — они подхватятся всем приложением.
    /// </summary>
    public static class AppConfig
    {
        // ========== Параметры сервера по умолчанию ==========
        // Отображаются в полях LoginWindow при запуске
        public const string DEFAULT_SERVER = "localhost";
        public const string DEFAULT_PORT = "5001";
        public const string DEFAULT_SCHEME = "https";

        // ========== Таймауты ==========
        public const int AUTH_TIMEOUT_SECONDS = 10;
        public const int API_TIMEOUT_SECONDS = 30;

        // ========== Пути API ==========
        // Префиксы
        public const string WPF_PREFIX = "/wpf";
        public const string API_PREFIX = "/api";

        // Аутентификация
        public const string AUTH_PATH = "/api/auth/login";

        // ========== Параметры загрузки видео ==========
        public const long MAX_VIDEO_FILE_SIZE = 2L * 1024 * 1024 * 1024; // 2 ГБ

        // ========== Валидация паролей ==========
        public const int PASSWORD_MIN_LENGTH = 6;
        public const int PASSWORD_MAX_LENGTH = 16;
        public const int USERNAME_MIN_LENGTH = 3;
        public const int USERNAME_MAX_LENGTH = 50;

        // ========== Пагинация по умолчанию ==========
        public const int DEFAULT_PAGE_SIZE = 20;
        public const int DEFAULT_PAGE = 1;

        // ========== Пути к ресурсам ==========
        public const string DEFAULT_POSTER_PATH = "/posters/default.jpg";
        public const string VLC_LIB_DIRECTORY = "libvlc/win-x64";

        // ========== Регулярные выражения ==========
        public const string EMAIL_REGEX = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";

        /// <summary>
        /// Собирает полный базовый URL из компонентов
        /// </summary>
        public static string BuildBaseUrl(string server, string port, string scheme = DEFAULT_SCHEME)
        {
            return $"{scheme}://{server}:{port}";
        }
    }
}
