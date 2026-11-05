using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SharedLib.Contracts.Admin;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using VideohostingPanel.Services;
using VideohostingPanel.ViewModels.Base;
using Vlc.DotNet.Core;
using Vlc.DotNet.Forms;

namespace VideohostingPanel.ViewModels.VerifyViewModels
{
    public partial class VerifyVideoViewModel : BaseViewModel
    {
        private readonly ApiService _apiService;
        private VlcControl? _vlcPlayer;
        private DispatcherTimer _updateTimer;
        public bool IsDragging = false;
        private volatile bool _isSeeking;

        private readonly object _seekLock = new object();
        private long _pendingSeekMs = -1;
        private bool _seekWorkerRunning;

        // Целевое время текущей перемотки и момент её выдачи воркеру.
        // Нужны, чтобы отличить «VLC доехал до цели» от «VLC ещё показывает
        // старое время» и не откатывать бегунок назад.
        private long _seekTargetMs = -1;
        private long _seekIssuedAt;

        // Эпоха перемотки. Увеличивается на каждый клик по ползунку и позволяет
        // отбросить события TimeChanged, которые VLC сгенерировал ДО клика:
        // они уже стояли в очереди диспетчера и, выполнившись после, затирали
        // выставленную пользователем позицию бегунка.
        private long _seekEpoch;

        // Окно, в течение которого после выдачи seek VLC ещё может присылать
        // времена из старого буфера (наблюдалось до ~150 мс). Подтверждением
        // считаем только время, пришедшее после этого окна.
        private const long SeekSettleMs = 600;

        // Допуск при подтверждении перемотки: VLC при точном seek всё равно
        // встаёт на ключевой кадр, поэтому точного совпадения не ждём.
        private const long SeekConfirmToleranceMs = 1500;

        // Сколько ждём подтверждения, прежде чем принудительно разблокировать бегунок.
        private const long SeekConfirmTimeoutMs = 4000;

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
            // Разворачивание выполняет View: libvlc не умеет разворачивать
            // embedded-видео в WinForms-хосте.
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

        #endregion

        #region Конструктор

        public VerifyVideoViewModel(ApiService apiService)
        {
            _apiService = apiService;

            _updateTimer = new DispatcherTimer();
            _updateTimer.Interval = TimeSpan.FromMilliseconds(500);
            _updateTimer.Tick += UpdateTimer_Tick;
        }

        #endregion

        #region Команды

        [RelayCommand]
        public async Task LoadVideo()
        {
            if (Video == null)
            {
                return;
            }

            try
            {
                if (!string.IsNullOrEmpty(Video.Poster))
                {
                    PosterImageSource = await PosterLoader.LoadAsync(Video.Id);
                }

                if (_vlcPlayer != null)
                {
                    var streamUrl = await _apiService.GetStreamUrlAsync(Video.Id);
                    _vlcPlayer.Play(new Uri(streamUrl));
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Ошибка загрузки: {ex.Message}";
                StatusColor = "#FF4444";
            }
        }

        [RelayCommand]
        public void PlayPause()
        {
            if (_vlcPlayer?.IsPlaying == true)
            {
                _vlcPlayer.Pause();
                IsPause = true;
            }
            else
            {
                _vlcPlayer?.Play();
                IsPause = false;
            }
        }

        [RelayCommand]
        public void SeekVideo(double position)
        {
            if (_vlcPlayer == null)
            {
                return;
            }

            var totalTime = _vlcPlayer.Length;

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
            // Порядок важен: сначала поднимаем флаг, потом сдвигаем эпоху. Тогда
            // любое событие TimeChanged, захваченное после этой точки, либо видит
            // _isSeeking, либо имеет устаревшую эпоху — и не затирает точку клика.
            _isSeeking = true;
            Volatile.Write(ref _seekIssuedAt, Environment.TickCount64);
            Interlocked.Increment(ref _seekEpoch);
            EnqueueSeek(newTime);
        }

        private void EnqueueSeek(long targetMs)
        {
            lock (_seekLock)
            {
                _pendingSeekMs = targetMs;
                _seekTargetMs = targetMs;
                _isSeeking = true;
                _seekIssuedAt = Environment.TickCount64;

                if (_seekWorkerRunning)
                {
                    return;
                }

                _seekWorkerRunning = true;
            }

            Task.Run(SeekWorker);
        }

        private void SeekWorker()
        {
            while (true)
            {
                long target;
                lock (_seekLock)
                {
                    if (_pendingSeekMs < 0)
                    {
                        // Цель выдана плееру, но ещё не подтверждена: _isSeeking
                        // снимает VlcPlayer_TimeChanged, когда VLC реально доедет
                        // до цели (или по таймауту в UpdateTimer_Tick).
                        _seekWorkerRunning = false;
                        return;
                    }

                    target = _pendingSeekMs;
                    _pendingSeekMs = -1;
                    _seekIssuedAt = Environment.TickCount64;
                }

                var player = _vlcPlayer;
                if (player == null)
                {
                    lock (_seekLock)
                    {
                        _seekWorkerRunning = false;
                        _isSeeking = false;
                        _seekTargetMs = -1;
                    }
                    return;
                }

                try
                {
                    player.Time = target;
                }
                catch
                {
                    // VLC может бросить IOException при переоткрытии потока — не фатально,
                    // пробуем через паузу.
                    try
                    {
                        var wasPlaying = player.IsPlaying;

                        if (wasPlaying)
                        {
                            player.Pause();
                        }

                        player.Time = target;

                        if (wasPlaying)
                        {
                            player.Play();
                        }
                    }
                    catch
                    {
                        // Поток уже переоткрыт/остановлен.
                    }
                }
            }
        }

        [RelayCommand]
        public void MuteUnmute()
        {
            if (_vlcPlayer != null)
            {
                IsMute = !IsMute;
                _vlcPlayer.Audio.IsMute = IsMute;
            }
        }

        [RelayCommand]
        public void VolumeChanged(double volume)
        {
            if (_vlcPlayer != null)
            {
                _vlcPlayer.Audio.Volume = (int)(volume * 100);
            }
        }

        [RelayCommand]
        public async Task Approve()
        {
            if (Video == null)
            {
                return;
            }

            try
            {
                await _apiService.VerifyVideoAsync(Video.Id);
                _ = _apiService.LogAsync(AuditTable.Video, AuditAction.Update, $"Видео верифицировано: «{Video.Name}» (id={Video.Id})");
            }
            catch (Exception ex)
            {
                StatusMessage = $"Ошибка: {ex.Message}";
                StatusColor = "#FF4444";
                return;
            }

            // После успешной верификации закрываем страницу так же, как по кнопке «Назад»,
            // иначе пользователь остаётся на уже обработанном видео.
            GoBack();
        }

        [RelayCommand]
        public void Reject()
        {
            GoBack();
        }

        [RelayCommand]
        public void GoBack()
        {
            _updateTimer?.Stop();

            // Сначала уходим со страницы, и только потом трогаем libvlc.
            // Если вызывать Stop() до навигации, зависший libvlc блокирует UI
            // на экране видео. Освобождение делает Cleanup() при выгрузке страницы.
            OnGoBackRequested?.Invoke();
        }

        #endregion

        #region Навигация

        public event Action? OnGoBackRequested;

        #endregion

        #region VLC Events

        private void VlcPlayer_Playing(object? sender, VlcMediaPlayerPlayingEventArgs e)
        {
            System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                IsVideoLoading = false;
                IsPause = false;
                if (_vlcPlayer != null)
                    _vlcPlayer.Audio.Volume = (int)(VolumeSliderPosition * 100);
                _updateTimer.Start();
            });
        }

        private void VlcPlayer_Paused(object? sender, VlcMediaPlayerPausedEventArgs e)
        {
            System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                IsPause = true;
            });
        }

        private void VlcPlayer_Opening(object? sender, VlcMediaPlayerOpeningEventArgs e)
        {
            System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                StatusMessage = "Загрузка видео...";
                StatusColor = "White";
            });
        }

        private void VlcPlayer_EncounteredError(object? sender, VlcMediaPlayerEncounteredErrorEventArgs e)
        {
            System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                IsVideoLoading = false;
                StatusMessage = "Не удалось воспроизвести видео";
                StatusColor = "#FF4444";
            });
        }

        private void VlcPlayer_Stopped(object? sender, VlcMediaPlayerStoppedEventArgs e)
        {
            System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                _updateTimer.Stop();
                IsPause = true;
            });
        }

        private void VlcPlayer_TimeChanged(object? sender, VlcMediaPlayerTimeChangedEventArgs e)
        {
            if (IsDragging)
                return;

            // Запоминаем эпоху на потоке VLC: если событие пришло раньше клика,
            // на UI оно уже не должно ничего менять.
            var epoch = Volatile.Read(ref _seekEpoch);

            // Перемотка ещё не подтверждена: VLC какое-то время продолжает отдавать
            // СТАРОЕ время, пока не переоткроет поток. Если обновлять бегунок по нему,
            // он откатывается назад и кажется, что клик не сработал (пользователь
            // жмёт повторно). Ждём, пока время реально дойдёт до цели И пройдёт
            // окно «устаканивания» — иначе первое же синхронное событие с точной
            // целью подтверждает перемотку, а следом приходит старое время из
            // буфера и всё равно затирает бегунок.
            if (_isSeeking)
            {
                var target = Volatile.Read(ref _seekTargetMs);
                var near = target >= 0 && Math.Abs(e.NewTime - target) <= SeekConfirmToleranceMs;
                var settled = IsPause || Environment.TickCount64 - Volatile.Read(ref _seekIssuedAt) >= SeekSettleMs;

                if (!near || !settled)
                    return;

                _isSeeking = false;
                Volatile.Write(ref _seekTargetMs, -1);
            }

            // Не биндим ползунок напрямую: пока пользователь тянет его, значение приходит
            // из VLC и вызывает Seek → зависание. Обновляем только вне перетаскивания.
            System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                // Событие захвачено до последнего клика: VLC успел поставить его
                // в очередь диспетчера раньше, чем пользователь перемотал. Такое
                // время устарело — иначе оно затирает точку клика.
                if (Volatile.Read(ref _seekEpoch) != epoch)
                {
                    return;
                }

                var currentTime = TimeSpan.FromMilliseconds(e.NewTime);
                CurrentTime = $"{currentTime.Minutes:D2}:{currentTime.Seconds:D2}";

                var player = _vlcPlayer;
                if (player == null)
                    return;

                var length = player.Length;

                if (length > 0)
                {
                    VideoSliderPosition = (double)e.NewTime / length;
                }
            }, System.Windows.Threading.DispatcherPriority.Background);
        }

        private void VlcPlayer_LengthChanged(object? sender, VlcMediaPlayerLengthChangedEventArgs e)
        {
            System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                var totalTime = TimeSpan.FromMilliseconds(e.NewLength);
                TotalTime = $"{totalTime.Minutes:D2}:{totalTime.Seconds:D2}";
            });
        }

        private void VlcPlayer_EndReached(object? sender, VlcMediaPlayerEndReachedEventArgs e)
        {
            // НЕ блокируем поток libvlc: Invoke из колбэка + Stop() на UI = deadlock.
            System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                IsPause = true;
                _vlcPlayer?.Stop();
            });
        }

        private void UpdateTimer_Tick(object? sender, EventArgs e)
        {
            // Страховка: если VLC так и не подтвердил перемотку (поток не переоткрылся,
            // видео на паузе и TimeChanged не приходит), снимаем блокировку по таймауту,
            // иначе бегунок останется «замороженным».
            if (_isSeeking && Environment.TickCount64 - Volatile.Read(ref _seekIssuedAt) > SeekConfirmTimeoutMs)
            {
                _isSeeking = false;
                Volatile.Write(ref _seekTargetMs, -1);
            }
        }

        #endregion

        #region Вспомогательные методы
        public void InitializeVlcPlayer(VlcControl vlcPlayer)
        {
            _disposed = false;
            _vlcPlayer = vlcPlayer;

            // Настройки — из одного источника с прогревом (VlcBootstrapper),
            // иначе прогрев разогревает одни плагины, а плеер просит другие.
            _vlcPlayer.VlcLibDirectory = VlcBootstrapper.LibDirectory;
            _vlcPlayer.VlcMediaplayerOptions = VlcBootstrapper.BuildOptions();

            _vlcPlayer.Playing += VlcPlayer_Playing;
            _vlcPlayer.Paused += VlcPlayer_Paused;
            _vlcPlayer.Stopped += VlcPlayer_Stopped;
            _vlcPlayer.TimeChanged += VlcPlayer_TimeChanged;
            _vlcPlayer.LengthChanged += VlcPlayer_LengthChanged;
            _vlcPlayer.EndReached += VlcPlayer_EndReached;
            _vlcPlayer.Opening += VlcPlayer_Opening;
            _vlcPlayer.EncounteredError += VlcPlayer_EncounteredError;
        }

        /// <summary>Идемпотентная очистка: вызывается и из View (Unloaded), и из MainWindow (закрытие страницы).</summary>
        public void Cleanup()
        {
            if (_disposed)
            {
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
                return;
            }

            if (IsFullScreen)
            {
                IsFullScreen = false;
            }

            _vlcPlayer.Playing -= VlcPlayer_Playing;
            _vlcPlayer.Paused -= VlcPlayer_Paused;
            _vlcPlayer.Stopped -= VlcPlayer_Stopped;
            _vlcPlayer.TimeChanged -= VlcPlayer_TimeChanged;
            _vlcPlayer.LengthChanged -= VlcPlayer_LengthChanged;
            _vlcPlayer.EndReached -= VlcPlayer_EndReached;
            _vlcPlayer.Opening -= VlcPlayer_Opening;
            _vlcPlayer.EncounteredError -= VlcPlayer_EncounteredError;

            try
            {
                _vlcPlayer.Stop();
            }
            catch
            {
                // Stop может бросить при уже освобождённом плеере.
            }

            try
            {
                _vlcPlayer.Dispose();
            }
            catch
            {
                // Dispose не должен мешать выгрузке страницы.
            }

            _vlcPlayer = null;
        }

        #endregion
    }
}