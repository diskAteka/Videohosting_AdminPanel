using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Vlc.DotNet.Core;
using WpfApiClient.Config;

namespace VideohostingPanel.Services
{
    /// <summary>
    /// Фоновый прогрев libvlc.
    ///
    /// Первый вызов libvlc_new в процессе занимает секунды: libvlc загружает
    /// libvlc.dll/libvlccore.dll и сканирует plugins (плюс антивирус проверяет
    /// каждый DLL). VlcControl.EndInit() создаёт инстанс синхронно в UI-потоке,
    /// поэтому без прогрева окно замирает при открытии видео.
    ///
    /// Прогрев создаёт инстанс в фоновом потоке и держит его до выхода из
    /// приложения: libvlc_unload освобождает DLL и убивает кэш плагинов.
    /// </summary>
    internal static class VlcBootstrapper
    {
        private static readonly Lazy<Task<bool>> WarmUpTask =
            new(() => Task.Run(RunWarmUp), LazyThreadSafetyMode.ExecutionAndPublication);

        private static VlcMediaPlayer? _keepAlive;

        /// <summary>
        /// Настройки плеера. Один источник правды: и прогрев, и VlcControl должны
        /// получать одинаковый набор опций.
        /// </summary>
        public static string[] BuildOptions()
        {
            var options = new List<string>
            {
                "--network-caching=3000",
                "--no-video-title-show",
                "--no-snapshot-preview"
                // --input-fast-seek не используем: он заставлял VLC прыгать на
                // ближайший ключевой кадр перед целью, из-за чего видео
                // начиналось раньше точки клика.
            };

            var trustDir = Path.Combine(AppContext.BaseDirectory, "Resources", "Certs");
            if (Directory.Exists(trustDir))
                options.Add($"--gnutls-dir-trust={trustDir}");

            return options.ToArray();
        }

        public static DirectoryInfo LibDirectory =>
            new(Path.Combine(AppContext.BaseDirectory, AppConfig.VLC_LIB_DIRECTORY));

        /// <summary>Запускает прогрев в фоне (если он ещё не запущен).</summary>
        public static void WarmUpInBackground()
        {
            _ = WarmUpTask.Value;
        }

        /// <summary>Ждёт завершения прогрева. Возвращает false, если прогрев не удался.</summary>
        public static Task<bool> WarmUpAsync() => WarmUpTask.Value;

        private static bool RunWarmUp()
        {
            try
            {
                _keepAlive = new VlcMediaPlayer(LibDirectory, BuildOptions());
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>Освобождает прогревочный инстанс. Вызывать при выходе из приложения.</summary>
        public static void Shutdown()
        {
            var player = Interlocked.Exchange(ref _keepAlive, null);
            if (player == null)
                return;

            try
            {
                player.Dispose();
            }
            catch
            {
                // Освобождение при выходе не должно ронять приложение.
            }
        }
    }
}