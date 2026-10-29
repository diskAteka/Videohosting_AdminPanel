using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Vlc.DotNet.Core;
using WpfApiClient.Config;

namespace VideohostingPanel.Services
{
    /// <summary>
    /// Прогрев libvlc (временный код диагностики/исправления).
    ///
    /// ПРОБЛЕМА: первый вызов libvlc_new в процессе занимает СЕКУНДЫ — libvlc загружает
    /// libvlc.dll/libvlccore.dll и сканирует plugins (плюс антивирус проверяет каждый DLL).
    /// Замер на этой машине: холодный старт 9384 мс, повторный 140-160 мс.
    /// Так как VlcControl.EndInit() создаёт инстанс синхронно в UI-потоке, приложение
    /// замирает на эти секунды — снаружи это выглядит как «зависание при открытии видео».
    ///
    /// РЕШЕНИЕ: один раз прогреваем libvlc в фоновом потоке (инстанс создаём и сразу
    /// освобождаем — нужен только эффект загрузки DLL и кэша плагинов в процессе).
    /// После прогрева EndInit занимает ~150 мс и UI не блокируется.
    ///
    /// Отключить: переменная окружения MYVIDEO_VLC_WARMUP=0.
    /// </summary>
    internal static class VlcBootstrapper
    {
        private static readonly Lazy<Task<WarmUpResult>> WarmUpTask =
            new(() => Task.Run(RunWarmUp), LazyThreadSafetyMode.ExecutionAndPublication);

        // 0 = не прогрет / не удалось, 1 = прогрет. Отдельное поле, чтобы IsWarm
        // можно было читать из UI-потока без ожидания задачи.
        private static int _warmOk;
        private static VlcMediaPlayer? _keepAlive;

        /// <summary>
        /// Прогрев уже завершён успешно. ВАЖНО: свойство не должно блокировать —
        /// раньше здесь был WarmUpTask.Value.Result, и обращение из UI-потока
        /// (лог в VM.InitializeVlcPlayer) ждало окончания прогрева, т.е. ровно того,
        /// от чего мы уходим.
        /// </summary>
        public static bool IsWarm => Volatile.Read(ref _warmOk) == 1;

        /// <summary>
        /// Настройки плеера. Один источник правды: и прогрев, и VlcControl должны
        /// получать одинаковый набор опций.
        /// </summary>
        public static string[] BuildOptions()
        {
            var options = new List<string>
            {
                "--network-caching=3000",   // Буферизация сети
                "--no-video-title-show",
                "--no-snapshot-preview"
                // --input-fast-seek убран: он заставлял VLC прыгать на ближайший
                // ключевой кадр ПЕРЕД целью, из-за чего видео начиналось заметно
                // раньше точки клика. Точность важнее скорости отклика.
            };

            var trustDir = Path.Combine(AppContext.BaseDirectory, "Resources", "Certs");
            if (Directory.Exists(trustDir))
                options.Add($"--gnutls-dir-trust={trustDir}");

            return options.ToArray();
        }

        public static DirectoryInfo LibDirectory =>
            new(Path.Combine(AppContext.BaseDirectory, AppConfig.VLC_LIB_DIRECTORY));

        /// <summary>Запускает прогрев в фоне (если он ещё не запущен). Не бросает исключений.</summary>
        public static void WarmUpInBackground()
        {
            if (Environment.GetEnvironmentVariable("MYVIDEO_VLC_WARMUP") == "0")
            {
                DiagLog.Write("VLC", "прогрев отключён через MYVIDEO_VLC_WARMUP=0");
                return;
            }

            _ = WarmUpTask.Value;
        }

        /// <summary>Ждёт завершения прогрева. Возвращает результат (в т.ч. при ошибке).</summary>
        public static Task<WarmUpResult> WarmUpAsync() => WarmUpTask.Value;

        private static WarmUpResult RunWarmUp()
        {
            var op = DiagLog.Begin("VlcBootstrapper.WarmUp");
            var sw = Stopwatch.StartNew();

            try
            {
                var libDir = LibDirectory;
                var options = BuildOptions();

                DiagLog.Mark("VlcBootstrapper.WarmUp", $"старт dir={libDir.FullName} options={options.Length}");

                // Инстанс НЕ освобождаем: libvlc_unload освобождает DLL и убивает
                // банк плагинов, после чего EndInit на UI-потоке снова платит за
                // загрузку. Держим его до выхода из приложения (Shutdown).
                _keepAlive = new VlcMediaPlayer(libDir, options);
                DiagLog.Mark("VlcBootstrapper.WarmUp", $"инстанс создан за {sw.ElapsedMilliseconds} мс");

                sw.Stop();
                Volatile.Write(ref _warmOk, 1);
                DiagLog.Write("VLC", $"прогрев libvlc завершён за {sw.ElapsedMilliseconds} мс");
                DiagLog.End(op, $"ok {sw.ElapsedMilliseconds}ms");

                return new WarmUpResult(true, sw.ElapsedMilliseconds, "");
            }
            catch (Exception ex)
            {
                sw.Stop();
                DiagLog.Write("EXC", "VlcBootstrapper.WarmUp: " + ex);
                DiagLog.End(op, "exception");
                return new WarmUpResult(false, sw.ElapsedMilliseconds, ex.GetType().Name + ": " + ex.Message);
            }
        }

        /// <summary>Освобождает прогревочный инстанс. Вызывать при выходе из приложения.</summary>
        public static void Shutdown()
        {
            var player = Interlocked.Exchange(ref _keepAlive, null);
            if (player == null)
                return;

            try { player.Dispose(); }
            catch (Exception ex) { DiagLog.Write("VLC", "Shutdown: " + ex.GetType().Name); }
        }

        internal readonly struct WarmUpResult
        {
            public WarmUpResult(bool ok, long elapsedMs, string error)
            {
                Ok = ok;
                ElapsedMs = elapsedMs;
                Error = error;
            }

            public bool Ok { get; }
            public long ElapsedMs { get; }
            public string Error { get; }
        }
    }
}
