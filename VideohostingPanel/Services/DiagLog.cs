using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using Application = System.Windows.Application;

namespace VideohostingPanel.Services
{
    /// <summary>
    /// Диагностика зависаний AdminPanel (временный тестовый код).
    ///
    /// Что делает:
    ///  1. Пишет подробный лог в logs/diag-YYYYMMDD.log (время, pid, id потока).
    ///  2. Опрашивает UI-поток «пульсом» через BeginInvoke. Если UI-поток не отвечает дольше
    ///     HangThresholdMs — считает, что приложение зависло.
    ///  3. При зависании пишет logs/hang-*.log: активные операции (BEGIN без END), состояние окон,
    ///     список потоков с native-состоянием, активность каждого потока, последние записи лога.
    ///  4. Пишет минидамп logs/dumps/hang-*.dmp — только он даёт точный стек UI-потока при deadlock.
    ///
    /// После починки багов файл можно удалить целиком вместе с вызовами DiagLog.*.
    /// </summary>
    internal static class DiagLog
    {
        /// <summary>Порог «UI-поток не отвечает» (мс).</summary>
        public const int HangThresholdMs = 5000;

        private const int PulseIntervalMs = 500;
        private const int MaxRecentEntries = 200;
        private const int ReportRepeatSeconds = 5;
        private static readonly int Pid = Environment.ProcessId;

        private static readonly object FileLock = new();
        private static readonly object StateLock = new();
        private static readonly List<string> Recent = new(MaxRecentEntries * 2);
        private static readonly ConcurrentDictionary<string, ActiveOperation> ActiveOps = new();
        private static readonly ConcurrentDictionary<int, ThreadInfo> Threads = new();

        private static string _logDir = Path.Combine(AppContext.BaseDirectory, "logs");
        private static string _logFile = "";
        // Тики, а не DateTime: поле пишется из UI-потока и читается сторожем,
        // а DateTime (8 байт) на x86 может порваться посередине записи.
        private static long _lastResponseTicks = DateTime.UtcNow.Ticks;
        private static DateTime _hangStartUtc = DateTime.MinValue;
        private static volatile bool _uiAlive = true;
        private static int _reportsWritten;
        private static int _started;
        private static long _dumpLock;
        private static int _dumpScheduled;

        /// <summary>
        /// Последний снимок состояния окон, снятый UI-потоком. Нужен потому, что
        /// Application.Windows нельзя читать из фонового потока (сторожа).
        /// </summary>
        private static volatile string _uiStateSnapshot = "нет снимка";

        public static bool IsStarted => _started == 1;

        private static DateTime LastResponseUtc =>
            new(Volatile.Read(ref _lastResponseTicks), DateTimeKind.Utc);

        private static void MarkUiResponse() =>
            Volatile.Write(ref _lastResponseTicks, DateTime.UtcNow.Ticks);

        #region Start

        /// <summary>Запускает диагностику. Вызывать один раз из UI-потока (App.OnStartup).</summary>
        public static void Start(string stage)
        {
            if (Interlocked.Exchange(ref _started, 1) == 1)
                return;

            EnsureLogDirectory();

            _logFile = Path.Combine(_logDir, $"diag-{DateTime.Now:yyyyMMdd}.log");

            Write("DIAG", $"=== START stage={stage} pid={Pid} exe={AppContext.BaseDirectory}");
            Write("DIAG", $"dir={_logDir} thresholdMs={HangThresholdMs} uiThread={Thread.CurrentThread.ManagedThreadId}");

            WriteEnvironment();

            var watchdog = new Thread(WatchdogLoop)
            {
                IsBackground = true,
                Name = "DiagWatchdog"
            };
            watchdog.Start();

            AppDomain.CurrentDomain.UnhandledException += (_, e) =>
                Write("EXC", "AppDomain.UnhandledException: " + e.ExceptionObject);
            AppDomain.CurrentDomain.FirstChanceException += (_, e) =>
            {
                // Только фатальные SEH — остальное даёт тонны шума.
                if (e.Exception is AccessViolationException or SEHException)
                    Write("EXC", "FirstChance(SEH): " + e.Exception);
            };
            AppDomain.CurrentDomain.ProcessExit += (_, _) => Write("DIAG", "ProcessExit");
            System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (_, e) =>
                Write("EXC", "UnobservedTaskException: " + e.Exception);

            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher != null)
            {
                dispatcher.UnhandledException += (_, e) =>
                    Write("EXC", "Dispatcher.UnhandledException: " + e.Exception);
                dispatcher.Hooks.DispatcherInactive += (_, _) => MarkUiResponse();
            }

            Write("DIAG", "=== START OK");

            StartSelfTestIfRequested();
        }

        /// <summary>
        /// ТЕСТОВЫЙ ТРИГГЕР. Запуск с переменной окружения MYVIDEO_DIAG_FREEZE=6000
        /// блокирует UI-поток на 6 секунд через 3 секунды после старта — так можно
        /// проверить, что сторож, hang-отчёт и минидамп действительно работают.
        /// Без переменной окружения ничего не делает.
        /// </summary>
        private static void StartSelfTestIfRequested()
        {
            var raw = Environment.GetEnvironmentVariable("MYVIDEO_DIAG_FREEZE");
            if (!int.TryParse(raw, out var freezeMs) || freezeMs <= 0)
                return;

            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null)
                return;

            Write("DIAG", $"SELF-TEST включён: блокировка UI на {freezeMs} мс через 3 с");

            var timer = new DispatcherTimer(DispatcherPriority.Normal, dispatcher)
            {
                Interval = TimeSpan.FromSeconds(3)
            };

            timer.Tick += (_, _) =>
            {
                timer.Stop();
                Write("DIAG", $"SELF-TEST: блокируем UI-поток на {freezeMs} мс");
                Thread.Sleep(freezeMs);
                Write("DIAG", "SELF-TEST: UI-поток отпущен");
            };

            timer.Start();
        }

        private static void EnsureLogDirectory()
        {
            try
            {
                Directory.CreateDirectory(_logDir);
                Directory.CreateDirectory(Path.Combine(_logDir, "dumps"));
                return;
            }
            catch
            {
                // Нет прав рядом с exe — уходим в %TEMP%.
            }

            try
            {
                _logDir = Path.Combine(Path.GetTempPath(), "MyVideoAdminPanel", "logs");
                Directory.CreateDirectory(_logDir);
                Directory.CreateDirectory(Path.Combine(_logDir, "dumps"));
            }
            catch { }
        }

        private static void WriteEnvironment()
        {
            Write("ENV", $"os={Environment.OSVersion} 64bit={Environment.Is64BitProcess} clr={Environment.Version} " +
                         $"cores={Environment.ProcessorCount}");
            Write("ENV", $"screen={SystemParameters.PrimaryScreenWidth}x{SystemParameters.PrimaryScreenHeight} " +
                         $"workArea={SystemParameters.WorkArea}");
            Write("ENV", "state: " + DescribeState());
        }

        #endregion

        #region Запись лога

        /// <summary>Запись в лог. Потокобезопасно, не бросает исключений.</summary>
        public static void Write(string category, string message)
        {
            try
            {
                var thread = Thread.CurrentThread;
                var line = $"[{DateTime.Now:HH:mm:ss.fff}] [{Pid}:{thread.ManagedThreadId}] {category,-5} {message}";

                TouchThread(thread, category, message);

                lock (FileLock)
                {
                    Recent.Add(line);
                    if (Recent.Count > MaxRecentEntries)
                        Recent.RemoveRange(0, Recent.Count - MaxRecentEntries);

                    if (!string.IsNullOrEmpty(_logFile))
                        File.AppendAllText(_logFile, line + Environment.NewLine, Encoding.UTF8);
                }
            }
            catch
            {
                // Диагностика не должна ломать приложение.
            }
        }

        private static void TouchThread(Thread thread, string category, string message)
        {
            var info = Threads.GetOrAdd(thread.ManagedThreadId, id => new ThreadInfo
            {
                ManagedThreadId = id,
                Name = SafeThreadName(thread),
                IsBackground = thread.IsBackground,
                StartedUtc = DateTime.UtcNow
            });

            info.LastSeenUtc = DateTime.UtcNow;
            info.LastCategory = category;
            info.LastMessage = Truncate(message, 160);
            info.Writes++;
        }

        private static string SafeThreadName(Thread t)
        {
            try { return t.Name ?? ""; } catch { return ""; }
        }

        private static string Truncate(string value, int max) =>
            value.Length <= max ? value : value[..max] + "…";

        #endregion

        #region Операции

        /// <summary>Начало потенциально блокирующей операции. Возвращает токен для <see cref="End"/>.</summary>
        public static string Begin(string op, string details = "")
        {
            var token = $"{op}#{Thread.CurrentThread.ManagedThreadId}#{Guid.NewGuid().ToString("N")[..6]}";
            ActiveOps[token] = new ActiveOperation
            {
                Name = op,
                Token = token,
                ThreadId = Thread.CurrentThread.ManagedThreadId,
                StartedUtc = DateTime.UtcNow,
                Details = details
            };

            Write("BEGIN", $"{op} tok={token} {details}");
            return token;
        }

        /// <summary>Завершение операции.</summary>
        public static void End(string token, string result = "ok")
        {
            if (string.IsNullOrEmpty(token))
                return;

            ActiveOps.TryRemove(token, out var op);
            var elapsed = op == null ? -1 : (long)(DateTime.UtcNow - op.StartedUtc).TotalMilliseconds;
            Write("END  ", $"{token} -> {result} elapsedMs={elapsed}");
        }

        /// <summary>Точка в коде: «мы здесь». Полезна как последний след перед блокировкой.</summary>
        public static void Mark(string where, string details = "")
        {
            Write("MARK ", string.IsNullOrEmpty(details) ? where : $"{where} | {details}");
        }

        /// <summary>Точка в коде с двумя деталями.</summary>
        public static void Mark(string where, string details, string extra)
        {
            Write("MARK ", $"{where} | {details} | {extra}");
        }

        /// <summary>
        /// Состояние окон/страницы в одну строку.
        /// ВАЖНО: Application.Windows можно читать ТОЛЬКО из UI-потока, иначе InvalidOperationException.
        /// Поэтому из фонового потока (сторож) возвращаем последний снимок, снятый UI-потоком.
        /// </summary>
        public static string DescribeState()
        {
            try
            {
                var app = Application.Current;
                if (app == null) return "app=null";

                if (!app.Dispatcher.CheckAccess())
                    return _uiStateSnapshot + " [снимок UI-потока, из фонового потока напрямую недоступно]";

                return CaptureStateUnsafe();
            }
            catch (Exception ex)
            {
                return "DescribeState failed: " + ex.GetType().Name + ": " + ex.Message;
            }
        }

        /// <summary>Снимок состояния. Вызывать только из UI-потока.</summary>
        private static string CaptureStateUnsafe()
        {
            try
            {
                var app = Application.Current;
                if (app == null) return "app=null";

                var windows = new List<string>();
                foreach (Window w in app.Windows)
                {
                    windows.Add($"{w.GetType().Name}(vis={w.IsVisible},state={w.WindowState}," +
                                $"topmost={w.Topmost},resize={w.ResizeMode}," +
                                $"rect={w.Left:0},{w.Top:0},{w.Width:0}x{w.Height:0})");
                }

                var main = app.MainWindow;
                var content = main?.Content as FrameworkElement;
                var state = $"windows=[{string.Join(" | ", windows)}] " +
                            $"mainContent={(content == null ? "null" : content.GetType().Name)}";
                state += " " + (main as Views.Windows.MainWindow)?.DescribeFrames();

                _uiStateSnapshot = state;
                return state;
            }
            catch (Exception ex)
            {
                return "CaptureState failed: " + ex.GetType().Name;
            }
        }

        #endregion

        #region Сторож

        private static void WatchdogLoop()
        {
            var lastRepeatUtc = DateTime.MinValue;

            while (true)
            {
                try
                {
                    Thread.Sleep(PulseIntervalMs);

                    var dispatcher = Application.Current?.Dispatcher;
                    if (dispatcher == null)
                        continue;

                    if (dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished)
                        break;

                    // Пульс: пустая операция в UI-потоке. Если очередь UI свободна — ответ придёт сразу.
                    // Заодно обновляем снимок состояния окон — из UI-потока это безопасно,
                    // а сторож потом прочитает готовую строку.
                    try
                    {
                        dispatcher.BeginInvoke(DispatcherPriority.Send, new Action(() =>
                        {
                            MarkUiResponse();
                            try { CaptureStateUnsafe(); } catch { }
                        }));
                    }
                    catch
                    {
                        // Dispatcher уже не работает.
                    }

                    var lastResponse = LastResponseUtc;
                    var silence = DateTime.UtcNow - lastResponse;

                    if (silence.TotalMilliseconds <= HangThresholdMs)
                    {
                        if (!_uiAlive)
                        {
                            var total = (DateTime.UtcNow - _hangStartUtc).TotalMilliseconds;
                            _uiAlive = true;
                            Write("HANG ", $"UI-поток ОТВЕТИЛ. Простой ~{total:0} мс. " +
                                           "Значит UI-поток блокировался, а не deadlock. " + _uiStateSnapshot);
                        }
                        continue;
                    }

                    if (_uiAlive)
                    {
                        _uiAlive = false;
                        _hangStartUtc = lastResponse;
                        lastRepeatUtc = DateTime.UtcNow;
                        Write("HANG ", $"UI-поток не отвечает {silence.TotalMilliseconds:0} мс " +
                                       $"(последний ответ {_hangStartUtc:HH:mm:ss.fff})");
                        WriteHangReport(silence);
                        // Дамп — отдельным потоком: dbghelp замораживает процесс.
                        ScheduleDump();
                        continue;
                    }

                    if ((DateTime.UtcNow - lastRepeatUtc).TotalSeconds >= ReportRepeatSeconds)
                    {
                        lastRepeatUtc = DateTime.UtcNow;
                        Write("HANG ", $"всё ещё висит, тишина {silence.TotalSeconds:0.0} с");
                        // Повторные отчёты — только текст: дамп уже снимается/снят.
                        WriteHangReport(silence);
                    }
                }
                catch (Exception ex)
                {
                    try { Write("DIAG", "watchdog error: " + ex); } catch { }
                }
            }
        }

        /// <summary>Отчёт о зависании: текстовый файл рядом с логом.</summary>
        private static void WriteHangReport(TimeSpan silence)
        {
            try
            {
                var id = Interlocked.Increment(ref _reportsWritten);
                var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss-fff");
                var reportPath = Path.Combine(_logDir, $"hang-{stamp}-{id}.log");

                // Папка лога может ещё не существовать (Write() не вызывался)
                // или быть удалена пользователем — создаём молча.
                Directory.CreateDirectory(_logDir);

                var sb = new StringBuilder();
                sb.AppendLine("======================= HANG REPORT =======================");
                sb.AppendLine($"time        : {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");
                sb.AppendLine($"pid         : {Pid}");
                sb.AppendLine($"silence     : {silence.TotalMilliseconds:0} ms (порог {HangThresholdMs} ms)");
                sb.AppendLine($"hangStart   : {_hangStartUtc:HH:mm:ss.fff} — последний ответ UI-потока");
                sb.AppendLine($"report      : #{id}");
                sb.AppendLine();

                sb.AppendLine("--- АКТИВНЫЕ ОПЕРАЦИИ (BEGIN без END) ---");
                AppendActiveOps(sb);
                sb.AppendLine();

                sb.AppendLine("--- ОКНА / РЕЖИМ (снимок UI-потока) ---");
                sb.AppendLine(_uiStateSnapshot);
                sb.AppendLine();

                sb.AppendLine("--- ПОТОКИ (native) ---");
                AppendNativeThreads(sb);
                sb.AppendLine();

                sb.AppendLine("--- ПОТОКИ (наша активность) ---");
                AppendOurThreads(sb);
                sb.AppendLine();

                sb.AppendLine($"--- ПОСЛЕДНИЕ {Recent.Count} ЗАПИСЕЙ ЛОГА ---");
                lock (FileLock)
                {
                    sb.AppendLine(string.Join(Environment.NewLine, Recent));
                }
                sb.AppendLine();

                // Отчёт пишем ДО дампа: если дамп упадёт или залипнет, отчёт уже на диске.
                File.AppendAllText(reportPath, sb.ToString(), Encoding.UTF8);
                Write("HANG ", "отчёт: " + reportPath);
            }
            catch (Exception ex)
            {
                try { Write("HANG ", "WriteHangReport failed: " + ex); } catch { }
            }
        }

        /// <summary>
        /// Снимает минидамп в отдельном потоке и только один раз за жизнь процесса.
        /// MiniDumpWriteDump приостанавливает ВСЕ потоки процесса, поэтому вызывать его
        /// прямо из сторожа нельзя: при первом зависании сторож навсегда оставался
        /// внутри dbghelp, дамп получался 0 байт, и отчёты больше не писались.
        /// </summary>
        private static void ScheduleDump()
        {
            if (Interlocked.Exchange(ref _dumpScheduled, 1) == 1)
                return;

            var thread = new Thread(() =>
            {
                try
                {
                    var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
                    var dumpPath = Path.Combine(_logDir, "dumps", $"hang-{stamp}.dmp");
                    Directory.CreateDirectory(Path.GetDirectoryName(dumpPath)!);

                    if (TryWriteMiniDump(dumpPath, out var error))
                        Write("HANG ", "минидамп: " + dumpPath);
                    else
                        Write("HANG ", "минидамп НЕ записан: " + error);
                }
                catch (Exception ex)
                {
                    try { Write("HANG ", "dump thread failed: " + ex.GetType().Name); } catch { }
                }
            })
            {
                IsBackground = true,
                Name = "DiagDumpWriter"
            };
            thread.Start();
        }

        private static void AppendActiveOps(StringBuilder sb)
        {
            if (ActiveOps.IsEmpty)
            {
                sb.AppendLine("  нет: зависание не внутри обёрнутого вызова");
                return;
            }

            foreach (var op in ActiveOps.Values.OrderBy(o => o.StartedUtc))
            {
                var elapsed = (DateTime.UtcNow - op.StartedUtc).TotalMilliseconds;
                sb.AppendLine($"  thread={op.ThreadId,-6} {op.Name,-32} {elapsed,8:0} ms  {op.Details}");
            }
        }

        private static void AppendNativeThreads(StringBuilder sb)
        {
            try
            {
                using var proc = Process.GetCurrentProcess();
                foreach (ProcessThread t in proc.Threads)
                {
                    string state;
                    try
                    {
                        state = $"{t.ThreadState} wait={t.WaitReason} cpu={t.TotalProcessorTime.TotalMilliseconds:0}ms";
                    }
                    catch
                    {
                        state = "unavailable";
                    }

                    sb.AppendLine($"  native {t.Id,-6} {state}");
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine("  Process.Threads error: " + ex.Message);
            }
        }

        private static void AppendOurThreads(StringBuilder sb)
        {
            foreach (var info in Threads.Values.OrderBy(t => t.ManagedThreadId))
            {
                var idle = (DateTime.UtcNow - info.LastSeenUtc).TotalMilliseconds;
                sb.AppendLine($"  managed {info.ManagedThreadId,-5} name='{info.Name}' bg={info.IsBackground} " +
                              $"writes={info.Writes} idle={idle:0}ms " +
                              $"last={info.LastCategory} {info.LastMessage}");
            }
        }

        #endregion

        #region MiniDump

        [DllImport("dbghelp.dll", SetLastError = true)]
        private static extern bool MiniDumpWriteDump(
            IntPtr hProcess, int processId, IntPtr hFile, int dumpType,
            IntPtr exceptionParam, IntPtr userStreamParam, IntPtr callbackParam);

        // 0x00000002 = MiniDumpWithFullMemory: стеки + куча. Для разбора managed-зависаний
        // в WinDbg (!clrstack / !syncblk) нужна именно память, MiniDumpNormal её не даёт.
        // 0x00001000 = MiniDumpWithThreadInfo: добавляет TEB.
        private const int MiniDumpWithFullMemory = 0x00000002;
        private const int MiniDumpWithThreadInfo = 0x00001000;

        /// <summary>
        /// Пишет полный минидамп. Вызывать ТОЛЬКО из потока ScheduleDump: dbghelp
        /// приостанавливает все потоки процесса, поэтому в UI-потоке и в стороже это
        /// гарантированный клинч.
        /// </summary>
        private static bool TryWriteMiniDump(string path, out string error)
        {
            error = "";
            if (Interlocked.Exchange(ref _dumpLock, 1) == 1)
            {
                error = "другой дамп уже пишется";
                return false;
            }

            try
            {
                using var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
                using var proc = Process.GetCurrentProcess();

                var written = MiniDumpWriteDump(proc.Handle, proc.Id, fs.SafeFileHandle.DangerousGetHandle(),
                    MiniDumpWithFullMemory | MiniDumpWithThreadInfo, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);

                if (!written)
                {
                    error = "MiniDumpWriteDump=false, Win32Error=" + Marshal.GetLastWin32Error();
                    return false;
                }

                fs.Flush();
                error = "";
                return true;
            }
            catch (Exception ex)
            {
                error = ex.GetType().Name + ": " + ex.Message;
                return false;
            }
            finally
            {
                Interlocked.Exchange(ref _dumpLock, 0);
            }
        }

        #endregion

        private sealed class ActiveOperation
        {
            public string Name { get; init; } = "";
            public string Token { get; init; } = "";
            public int ThreadId { get; init; }
            public DateTime StartedUtc { get; init; }
            public string Details { get; init; } = "";
        }

        private sealed class ThreadInfo
        {
            public int ManagedThreadId { get; init; }
            public string Name { get; init; } = "";
            public bool IsBackground { get; init; }
            public DateTime StartedUtc { get; init; }
            public DateTime LastSeenUtc { get; set; }
            public string LastCategory { get; set; } = "";
            public string LastMessage { get; set; } = "";
            public int Writes { get; set; }
        }
    }
}
