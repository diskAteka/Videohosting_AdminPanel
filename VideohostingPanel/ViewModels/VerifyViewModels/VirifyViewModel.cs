using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SharedLib.Contracts.Admin;
using System.IO;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using VideohostingPanel.Services;
using VideohostingPanel.ViewModels.Base;
using Vlc.DotNet.Core;
using Vlc.DotNet.Forms;
using WpfApiClient.Config;

namespace VideohostingPanel.ViewModels.VerifyViewModels
{
    public partial class VerifyVideoViewModel : BaseViewModel
    {
        private readonly ApiService _apiService;
        private VlcControl _vlcPlayer;
        private DispatcherTimer _updateTimer;
        public bool IsDragging = false;
        private volatile bool _isSeeking;

        private readonly object _seekLock = new object();
        private long _pendingSeekMs = -1;
        private bool _seekWorkerRunning;

        // ДИАГНОСТИКА: защита от повторной очистки (страница может выгружаться дважды).
        private bool _disposed;

        #region Свойства для разметки

        [ObservableProperty]
        private BitmapImage? _posterImageSource;

        [ObservableProperty]
        private GetVideoResponse? _video;

        [ObservableProperty]
        private double _videoSliderPosition;

        [ObservableProperty]
        private double _volumeSliderPosition = 0.8;

        [ObservableProperty]
        private bool _isPause;

        [ObservableProperty]
        private bool _isMute;

        [ObservableProperty]
        private string _currentTime = "00:00";

        [ObservableProperty]
        private string _totalTime = "00:00";

        [ObservableProperty]
        private bool _isVideoLoading = true;

        [ObservableProperty]
        private bool _isFullScreen;

        partial void OnIsFullScreenChanged(bool value)
        {
            // Само разворачивание выполняет View (VirifyVideo.xaml.cs):
            // libvlc не умеет разворачивать embedded-видео в WinForms-хосте.
            FullScreenChanged?.Invoke(value);
        }

        public event Action<bool>? FullScreenChanged;

        [RelayCommand]
        public void ToggleFullScreen()
        {
            IsFullScreen = !IsFullScreen;
        }

        [ObservableProperty]
        private string _statusMessage = "";

        [ObservableProperty]
        private string _statusColor = "White";

        [ObservableProperty]
        private string _rejectReason = "";

        [ObservableProperty]
        private bool _isRejectReasonVisible;

        #endregion

        #region Конструктор

        public VerifyVideoViewModel(ApiService apiService)
        {
            _apiService = apiService;

            // Таймер для обновления времени
            _updateTimer = new DispatcherTimer();
            _updateTimer.Interval = TimeSpan.FromMilliseconds(500);
            _updateTimer.Tick += UpdateTimer_Tick;
        }

        #endregion

        #region Команды

        [RelayCommand]
        public async Task LoadVideo()
        {
            // ДИАГНОСТИКА: тут подозрение на зависание (poster + stream url + VLC Play).
            var op = DiagLog.Begin("VM.LoadVideo", $"videoId={Video?.Id} thread={Environment.CurrentManagedThreadId}");
            try
            {
                if (!string.IsNullOrEmpty(Video.Poster))
                {
                    DiagLog.Mark("VM.LoadVideo", "before PosterLoader");
                    PosterImageSource = await PosterLoader.LoadAsync(Video.Id);
                    DiagLog.Mark("VM.LoadVideo", "after PosterLoader");
                }

                if (_vlcPlayer != null)
                {
                    DiagLog.Mark("VM.LoadVideo", "before GetStreamUrlAsync");
                    var streamUrl = await _apiService.GetStreamUrlAsync(Video.Id);
                    DiagLog.Mark("VM.LoadVideo", "after GetStreamUrlAsync", "play");

                    _vlcPlayer.Play(new Uri(streamUrl));
                    DiagLog.Mark("VM.LoadVideo", "after VLC Play");
                }
                else
                {
                    DiagLog.Mark("VM.LoadVideo", "_vlcPlayer == null — Play не вызван");
                }

                DiagLog.End(op);
            }
            catch (Exception ex)
            {
                DiagLog.Write("EXC", "VM.LoadVideo: " + ex);
                DiagLog.End(op, "exception");
                StatusMessage = $"Ошибка загрузки: {ex.Message}";
                StatusColor = "#FF4444";
            }
        }

        [RelayCommand]
        public void PlayPause()
        {
            var op = DiagLog.Begin("VM.PlayPause");
            try
            {
                if (_vlcPlayer?.IsPlaying == true)
                {
                    DiagLog.Mark("VM.PlayPause", "before Pause");
                    _vlcPlayer.Pause();
                    DiagLog.Mark("VM.PlayPause", "after Pause");
                    IsPause = true;
                }
                else
                {
                    DiagLog.Mark("VM.PlayPause", "before Play (resume)");
                    _vlcPlayer?.Play();
                    DiagLog.Mark("VM.PlayPause", "after Play (resume)");
                    IsPause = false;
                }
            }
            finally
            {
                DiagLog.End(op);
            }
        }

        [RelayCommand]
        public void SeekVideo(double position)
        {
            if (_vlcPlayer == null)
            {
                DiagLog.Mark("VM.SeekVideo", "_vlcPlayer == null");
                return;
            }

            // ДИАГНОСТИКА: чтение Length на UI-потоке — подозреваемый №1
            // (libvlc берёт input-lock и может ждать переоткрытия сетевого потока).
            var op = DiagLog.Begin("VM.SeekVideo", $"position={position:0.000}");
            try
            {
                DiagLog.Mark("VM.SeekVideo", "before _vlcPlayer.Length");
                var totalTime = _vlcPlayer.Length;
                DiagLog.Mark("VM.SeekVideo", $"after Length={totalTime}");

                if (totalTime <= 0)
                    return;

                if (position < 0) position = 0;
                if (position > 1) position = 1;

                var newTime = (long)(totalTime * position);

                // Не даём перемотке попасть ровно в конец: VLC в этот момент
                // останавливает воспроизведение и обрывает сетевой поток.
                if (totalTime > 1000 && newTime > totalTime - 1000)
                    newTime = totalTime - 1000;

                VideoSliderPosition = position;

                // Перемотка сетевого потока занимает время (VLC переоткрывает соединение
                // с S3), поэтому выполняется в фоне. Запросы сериализуются: пока идёт
                // предыдущая перемотка, новый запрос только заменяет целевое время.
                EnqueueSeek(newTime);
                DiagLog.Mark("VM.SeekVideo", $"enqueued {newTime}ms");
            }
            finally
            {
                DiagLog.End(op);
            }
        }

        private void EnqueueSeek(long targetMs)
        {
            lock (_seekLock)
            {
                _pendingSeekMs = targetMs;
                _isSeeking = true;

                if (_seekWorkerRunning)
                {
                    DiagLog.Mark("VM.EnqueueSeek", "worker уже работает, только заменили цель");
                    return;
                }

                _seekWorkerRunning = true;
            }

            DiagLog.Mark("VM.EnqueueSeek", $"запуск SeekWorker, target={targetMs}");
            Task.Run(SeekWorker);
        }

        private void SeekWorker()
        {
            DiagLog.Write("SEEK", "SeekWorker: старт (thread pool)");
            while (true)
            {
                long target;
                lock (_seekLock)
                {
                    if (_pendingSeekMs < 0)
                    {
                        _seekWorkerRunning = false;
                        _isSeeking = false;
                        DiagLog.Write("SEEK", "SeekWorker: очередь пуста, выход");
                        return;
                    }

                    target = _pendingSeekMs;
                    _pendingSeekMs = -1;
                }

                var player = _vlcPlayer;
                if (player == null)
                {
                    // ДИАГНОСТИКА: раньше здесь был бесконечный busy-loop при Dispose плеера.
                    DiagLog.Write("SEEK", "SeekWorker: player == null — выходим, чтобы не крутить холостой цикл");
                    lock (_seekLock)
                    {
                        _seekWorkerRunning = false;
                        _isSeeking = false;
                    }
                    return;
                }

                var op = DiagLog.Begin("SeekWorker.player.Time", $"target={target}ms");
                try
                {
                    DiagLog.Mark("SeekWorker", "before player.Time = target");
                    player.Time = target;
                    DiagLog.Mark("SeekWorker", "after player.Time = target");
                    DiagLog.End(op);
                }
                catch (Exception ex)
                {
                    DiagLog.Write("SEEK", "SeekWorker: player.Time бросил " + ex.GetType().Name + ": " + ex.Message);

                    // VLC может бросить IOException при переоткрытии потока — не фатально.
                    var fallback = DiagLog.Begin("SeekWorker.fallback", $"target={target}ms");
                    try
                    {
                        DiagLog.Mark("SeekWorker", "fallback: before IsPlaying");
                        var wasPlaying = player.IsPlaying;
                        DiagLog.Mark("SeekWorker", $"fallback: IsPlaying={wasPlaying}");

                        if (wasPlaying)
                        {
                            DiagLog.Mark("SeekWorker", "fallback: before Pause");
                            player.Pause();
                            DiagLog.Mark("SeekWorker", "fallback: after Pause");
                        }

                        DiagLog.Mark("SeekWorker", "fallback: before player.Time");
                        player.Time = target;
                        DiagLog.Mark("SeekWorker", "fallback: after player.Time");

                        if (wasPlaying)
                        {
                            DiagLog.Mark("SeekWorker", "fallback: before Play");
                            player.Play();
                            DiagLog.Mark("SeekWorker", "fallback: after Play");
                        }
                    }
                    catch (Exception fallbackEx)
                    {
                        // Поток уже переоткрыт/остановлен.
                        DiagLog.Write("SEEK", "SeekWorker fallback бросил: " + fallbackEx.GetType().Name);
                    }
                    finally
                    {
                        DiagLog.End(fallback, "fallback done");
                        DiagLog.End(op, "exception");
                    }
                }
            }
        }

        [RelayCommand]
        public void MuteUnmute()
        {
            if (_vlcPlayer != null)
            {
                var op = DiagLog.Begin("VM.MuteUnmute");
                try
                {
                    IsMute = !IsMute;
                    DiagLog.Mark("VM.MuteUnmute", $"before Audio.IsMute={IsMute}");
                    _vlcPlayer.Audio.IsMute = IsMute;
                    DiagLog.Mark("VM.MuteUnmute", "after Audio.IsMute");
                }
                finally
                {
                    DiagLog.End(op);
                }
            }
        }

        [RelayCommand]
        public void VolumeChanged(double volume)
        {
            if (_vlcPlayer != null)
            {
                var op = DiagLog.Begin("VM.VolumeChanged", $"volume={volume:0.00}");
                try
                {
                    DiagLog.Mark("VM.VolumeChanged", "before Audio.Volume");
                    _vlcPlayer.Audio.Volume = (int)(volume * 100);
                    DiagLog.Mark("VM.VolumeChanged", "after Audio.Volume");
                }
                finally
                {
                    DiagLog.End(op);
                }
            }
        }

        [RelayCommand]
        public async Task Approve()
        {
            // ДИАГНОСТИКА: жалоба «зависает при нажатии Принять».
            var op = DiagLog.Begin("VM.Approve", $"videoId={Video?.Id}");
            try
            {
                if (Video == null)
                {
                    DiagLog.Mark("VM.Approve", "Video == null — выходим");
                    return;
                }

                try
                {
                    DiagLog.Mark("VM.Approve", "before VerifyVideoAsync");
                    await _apiService.VerifyVideoAsync(Video.Id);
                    DiagLog.Mark("VM.Approve", "after VerifyVideoAsync");
                }
                catch (Exception ex)
                {
                    DiagLog.Write("EXC", "VM.Approve HTTP: " + ex);
                    StatusMessage = $"Ошибка: {ex.Message}";
                    StatusColor = "#FF4444";
                    return;
                }

                // После успешной верификации закрываем страницу так же, как по кнопке «Назад»,
                // иначе пользователь остаётся на уже обработанном видео.
                DiagLog.Mark("VM.Approve", "before GoBack");
                GoBack();
                DiagLog.Mark("VM.Approve", "after GoBack");
            }
            finally
            {
                DiagLog.End(op);
            }
        }

        [RelayCommand]
        public async Task Reject()
        {
            var op = DiagLog.Begin("VM.Reject", $"videoId={Video?.Id} reasonLen={RejectReason?.Length ?? 0}");
            try
            {
                DiagLog.Mark("VM.Reject", "before GoBack");
                GoBack();
                DiagLog.Mark("VM.Reject", "after GoBack");
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                DiagLog.Write("EXC", "VM.Reject: " + ex);
                StatusMessage = $"Ошибка: {ex.Message}";
                StatusColor = "#FF4444";
            }
            finally
            {
                DiagLog.End(op);
            }
        }

        [RelayCommand]
        public void GoBack()
        {
            var op = DiagLog.Begin("VM.GoBack");
            try
            {
                _updateTimer?.Stop();

                // ВАЖНО: сначала уходим со страницы, и только потом трогаем libvlc.
                // Раньше Stop() вызывался первым — на UI-потоке, до навигации:
                // если libvlc зависал, приложение намертво замирало на экране видео.
                // Теперь пользователь мгновенно возвращается к списку, а плеер
                // освобождает Cleanup() при выгрузке страницы.
                DiagLog.Mark("VM.GoBack", "before OnGoBackRequested");
                OnGoBackRequested?.Invoke();
                DiagLog.Mark("VM.GoBack", "after OnGoBackRequested (страница скрыта)");
            }
            finally
            {
                DiagLog.End(op);
            }
        }

        #endregion

        #region Навигация

        public event Action? OnGoBackRequested;

        #endregion

        #region VLC Events

        private void VlcPlayer_Playing(object? sender, VlcMediaPlayerPlayingEventArgs e)
        {
            DiagLog.Mark("VLC.Playing", "event fired");
            System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                DiagLog.Mark("VLC.Playing", "inside Dispatcher.Invoke");
                IsVideoLoading = false;
                IsPause = false;
                if (_vlcPlayer != null)
                    _vlcPlayer.Audio.Volume = (int)(VolumeSliderPosition * 100);
                _updateTimer.Start();
            });
        }

        private void VlcPlayer_Paused(object? sender, VlcMediaPlayerPausedEventArgs e)
        {
            DiagLog.Mark("VLC.Paused", "event fired");
            System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                IsPause = true;
            });
        }

        private void VlcPlayer_Opening(object? sender, VlcMediaPlayerOpeningEventArgs e)
        {
            DiagLog.Mark("VLC.Opening", "event fired");
            System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                StatusMessage = "Загрузка видео...";
                StatusColor = "White";
            });
        }

        private void VlcPlayer_EncounteredError(object? sender, VlcMediaPlayerEncounteredErrorEventArgs e)
        {
            DiagLog.Write("VLC", "EncounteredError event fired");
            System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                IsVideoLoading = false;
                StatusMessage = "Не удалось воспроизвести видео";
                StatusColor = "#FF4444";
            });
        }

        private void VlcPlayer_Stopped(object? sender, VlcMediaPlayerStoppedEventArgs e)
        {
            DiagLog.Mark("VLC.Stopped", "event fired");
            System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                _updateTimer.Stop();
                IsPause = true;
            });
        }

        private void VlcPlayer_TimeChanged(object? sender, VlcMediaPlayerTimeChangedEventArgs e)
        {
            if (IsDragging || _isSeeking)
                return;

            // Не биндим ползунок напрямую: пока пользователь тянет его, значение приходит
            // из VLC и вызывает Seek → зависание. Обновляем только вне перетаскивания.
            System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                // ДИАГНОСТИКА: _vlcPlayer.Length читается на UI-потоке ~1 раз/сек.
                // Если UI подвиснет — в hang-логе останется BEGIN без END для UI.TimeChanged.
                var op = DiagLog.Begin("UI.TimeChanged", $"newTime={e.NewTime}");
                try
                {
                    var currentTime = TimeSpan.FromMilliseconds(e.NewTime);
                    CurrentTime = $"{currentTime.Minutes:D2}:{currentTime.Seconds:D2}";

                    var player = _vlcPlayer;
                    if (player == null)
                        return;

                    DiagLog.Mark("UI.TimeChanged", "before _vlcPlayer.Length");
                    var length = player.Length;
                    DiagLog.Mark("UI.TimeChanged", $"after Length={length}");

                    if (length > 0)
                    {
                        VideoSliderPosition = (double)e.NewTime / length;
                    }
                }
                finally
                {
                    DiagLog.End(op);
                }
            }, System.Windows.Threading.DispatcherPriority.Background);
        }

        private void VlcPlayer_LengthChanged(object? sender, VlcMediaPlayerLengthChangedEventArgs e)
        {
            DiagLog.Mark("VLC.LengthChanged", $"newLength={e.NewLength}");
            System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                var totalTime = TimeSpan.FromMilliseconds(e.NewLength);
                TotalTime = $"{totalTime.Minutes:D2}:{totalTime.Seconds:D2}";
            });
        }

        private void VlcPlayer_EndReached(object? sender, VlcMediaPlayerEndReachedEventArgs e)
        {
            DiagLog.Mark("VLC.EndReached", "event fired");
            // НЕ блокируем поток libvlc: Invoke из колбэка + Stop() на UI = deadlock.
            System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                DiagLog.Mark("VLC.EndReached", "inside InvokeAsync, before Stop");
                IsPause = true;
                _vlcPlayer?.Stop();
                DiagLog.Mark("VLC.EndReached", "after Stop");
            });
        }

        private void UpdateTimer_Tick(object? sender, EventArgs e)
        {
            // Дополнительные обновления если нужно
        }

        #endregion

        #region Вспомогательные методы
        public void InitializeVlcPlayer(VlcControl vlcPlayer)
        {
            var op = DiagLog.Begin("VM.InitializeVlcPlayer");
            try
            {
                _disposed = false;
                _vlcPlayer = vlcPlayer;
                DiagLog.Mark("VM.InitializeVlcPlayer", "VlcControl assigned", $"vlcControlsAlive={VlcControlTracker.Alive}");

                // Настройки — из одного источника с прогревом (VlcBootstrapper),
                // иначе прогрев разогревает одни плагины, а плеер просит другие.
                _vlcPlayer.VlcLibDirectory = VlcBootstrapper.LibDirectory;
                _vlcPlayer.VlcMediaplayerOptions = VlcBootstrapper.BuildOptions();

                DiagLog.Mark("VM.InitializeVlcPlayer", $"warm={VlcBootstrapper.IsWarm}");

                // Подписка на события плеера
                _vlcPlayer.Playing += VlcPlayer_Playing;
                _vlcPlayer.Paused += VlcPlayer_Paused;
                _vlcPlayer.Stopped += VlcPlayer_Stopped;
                _vlcPlayer.TimeChanged += VlcPlayer_TimeChanged;
                _vlcPlayer.LengthChanged += VlcPlayer_LengthChanged;
                _vlcPlayer.EndReached += VlcPlayer_EndReached;
                _vlcPlayer.Opening += VlcPlayer_Opening;
                _vlcPlayer.EncounteredError += VlcPlayer_EncounteredError;

                DiagLog.Mark("VM.InitializeVlcPlayer", "events subscribed");
            }
            finally
            {
                DiagLog.End(op);
            }
        }

        /// <summary>Идемпотентная очистка: вызывается и из View (Unloaded), и из MainWindow (закрытие страницы).</summary>
        public void Cleanup()
        {
            var op = DiagLog.Begin("VM.Cleanup", $"alreadyDisposed={_disposed}");
            try
            {
                if (_disposed)
                {
                    DiagLog.Mark("VM.Cleanup", "повторный вызов — игнорируем");
                    return;
                }

                _disposed = true;
                _updateTimer?.Stop();

                lock (_seekLock)
                {
                    _pendingSeekMs = -1;
                }

                if (_vlcPlayer == null)
                {
                    DiagLog.Mark("VM.Cleanup", "_vlcPlayer == null — нечего освобождать");
                    return;
                }

                if (IsFullScreen)
                {
                    DiagLog.Mark("VM.Cleanup", "IsFullScreen -> false");
                    IsFullScreen = false;
                }

                DiagLog.Mark("VM.Cleanup", "unsubscribing VLC events");
                _vlcPlayer.Playing -= VlcPlayer_Playing;
                _vlcPlayer.Paused -= VlcPlayer_Paused;
                _vlcPlayer.Stopped -= VlcPlayer_Stopped;
                _vlcPlayer.TimeChanged -= VlcPlayer_TimeChanged;
                _vlcPlayer.LengthChanged -= VlcPlayer_LengthChanged;
                _vlcPlayer.EndReached -= VlcPlayer_EndReached;
                _vlcPlayer.Opening -= VlcPlayer_Opening;
                _vlcPlayer.EncounteredError -= VlcPlayer_EncounteredError;

                // ДИАГНОСТИКА: Stop/Dispose на UI-потоке — главный подозреваемый в «вечном» зависании.
                // Каждый шаг пишется отдельно: если зависание здесь — в hang-логе будет
                // BEGIN без END для VM.Cleanup + минидамп со стеком.
                var stopOp = DiagLog.Begin("VM.Cleanup.Stop");
                try
                {
                    DiagLog.Mark("VM.Cleanup", "before VLC Stop");
                    _vlcPlayer.Stop();
                    DiagLog.Mark("VM.Cleanup", "after VLC Stop");
                    DiagLog.End(stopOp);
                }
                catch (Exception ex)
                {
                    DiagLog.Write("EXC", "VM.Cleanup Stop: " + ex);
                    DiagLog.End(stopOp, "exception");
                }

                var disposeOp = DiagLog.Begin("VM.Cleanup.Dispose");
                try
                {
                    DiagLog.Mark("VM.Cleanup", "before VLC Dispose");
                    _vlcPlayer.Dispose();
                    DiagLog.Mark("VM.Cleanup", "after VLC Dispose", $"vlcControlsAlive={VlcControlTracker.Alive}");
                    DiagLog.End(disposeOp);
                }
                catch (Exception ex)
                {
                    DiagLog.Write("EXC", "VM.Cleanup Dispose: " + ex);
                    DiagLog.End(disposeOp, "exception");
                }

                _vlcPlayer = null;
            }
            finally
            {
                DiagLog.End(op);
            }
        }

        #endregion
    }
}