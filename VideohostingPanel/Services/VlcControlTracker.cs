using System.Threading;

namespace VideohostingPanel.Services
{
    /// <summary>
    /// ДИАГНОСТИКА: счётчик живых VlcControl/libvlc-инстансов.
    /// Если Alive растёт при каждом открытии страницы проверки видео — утечка,
    /// а вместе с ней и деградация до зависаний.
    /// </summary>
    internal static class VlcControlTracker
    {
        private static int _created;
        private static int _disposed;

        public static int CreatedCount => Volatile.Read(ref _created);
        public static int DisposedCount => Volatile.Read(ref _disposed);
        public static int Alive => CreatedCount - DisposedCount;

        public static void OnCreated()
        {
            Interlocked.Increment(ref _created);
            DiagLog.Write("VLC", $"VlcControl создан, живых={Alive}");
        }

        public static void OnDisposed()
        {
            Interlocked.Increment(ref _disposed);
            DiagLog.Write("VLC", $"VlcControl освобождён, живых={Alive}");
        }
    }
}
