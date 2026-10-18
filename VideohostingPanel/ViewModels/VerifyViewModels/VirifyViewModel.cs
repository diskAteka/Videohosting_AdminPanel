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
            try
            {
                if (!string.IsNullOrEmpty(Video.Poster))
                    PosterImageSource = await PosterLoader.LoadAsync(Video.Id);

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
            if (_vlcPlayer != null)
            {
                var totalTime = _vlcPlayer.Length;
                var newTime = (long)(totalTime * position);
                _vlcPlayer.Time = newTime;
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
            try
            {
                if (Video == null) return;

                await _apiService.VerifyVideoAsync(Video.Id);
                StatusMessage = "Видео успешно одобрено!";
                StatusColor = "#4CAF50";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Ошибка: {ex.Message}";
                StatusColor = "#FF4444";
            }
        }

        [RelayCommand]
        public async Task Reject()
        {
            try
            {
                GoBack();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Ошибка: {ex.Message}";
                StatusColor = "#FF4444";
            }
        }

        [RelayCommand]
        public void GoBack()
        {
            _vlcPlayer?.Stop();
            _updateTimer?.Stop();
            OnGoBackRequested?.Invoke();
        }

        #endregion

        #region Навигация

        public event Action? OnGoBackRequested;

        #endregion

        #region VLC Events

        private void VlcPlayer_Playing(object sender, VlcMediaPlayerPlayingEventArgs e)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                IsVideoLoading = false;
                IsPause = false;
                _updateTimer.Start();
            });
        }

        private void VlcPlayer_Paused(object sender, VlcMediaPlayerPausedEventArgs e)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                IsPause = true;
            });
        }

        private void VlcPlayer_Opening(object sender, VlcMediaPlayerOpeningEventArgs e)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                StatusMessage = "Загрузка видео...";
                StatusColor = "White";
            });
        }

        private void VlcPlayer_EncounteredError(object sender, VlcMediaPlayerEncounteredErrorEventArgs e)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                IsVideoLoading = false;
                StatusMessage = "Не удалось воспроизвести видео";
                StatusColor = "#FF4444";
            });
        }

        private void VlcPlayer_Stopped(object sender, VlcMediaPlayerStoppedEventArgs e)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                _updateTimer.Stop();
                IsPause = true;
            });
        }

        private void VlcPlayer_TimeChanged(object sender, VlcMediaPlayerTimeChangedEventArgs e)
        {
            if (IsDragging)
                return;

            // Не биндим ползунок напрямую: пока пользователь тянет его, значение приходит
            // из VLC и вызывает Seek → зависание. Обновляем только вне перетаскивания.
            System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                var currentTime = TimeSpan.FromMilliseconds(e.NewTime);
                CurrentTime = $"{currentTime.Minutes:D2}:{currentTime.Seconds:D2}";

                if (_vlcPlayer.Length > 0)
                {
                    VideoSliderPosition = (double)e.NewTime / _vlcPlayer.Length;
                }
            }, System.Windows.Threading.DispatcherPriority.Background);
        }

        private void VlcPlayer_LengthChanged(object sender, VlcMediaPlayerLengthChangedEventArgs e)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                var totalTime = TimeSpan.FromMilliseconds(e.NewLength);
                TotalTime = $"{totalTime.Minutes:D2}:{totalTime.Seconds:D2}";
            });
        }

        private void VlcPlayer_EndReached(object sender, VlcMediaPlayerEndReachedEventArgs e)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                IsPause = true;
                _vlcPlayer.Stop();
            });
        }

        private void UpdateTimer_Tick(object sender, EventArgs e)
        {
            // Дополнительные обновления если нужно
        }

        #endregion

        #region Вспомогательные методы
        public void InitializeVlcPlayer(VlcControl vlcPlayer)
        {
            _vlcPlayer = vlcPlayer;

            _vlcPlayer.VlcLibDirectory = new DirectoryInfo(
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, 
                AppConfig.VLC_LIB_DIRECTORY));

            var options = new List<string>
            {
                "--network-caching=3000",  // Буферизация сети
                "--no-video-title-show",
                "--no-snapshot-preview"
            };

            var trustDir = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "Resources", "Certs");

            if (Directory.Exists(trustDir))
                options.Add($"--gnutls-dir-trust={trustDir}");

            _vlcPlayer.VlcMediaplayerOptions = options.ToArray();

            // Подписка на события плеера
            _vlcPlayer.Playing += VlcPlayer_Playing;
            _vlcPlayer.Paused += VlcPlayer_Paused;
            _vlcPlayer.Stopped += VlcPlayer_Stopped;
            _vlcPlayer.TimeChanged += VlcPlayer_TimeChanged;
            _vlcPlayer.LengthChanged += VlcPlayer_LengthChanged;
            _vlcPlayer.EndReached += VlcPlayer_EndReached;
            _vlcPlayer.Opening += VlcPlayer_Opening;
            _vlcPlayer.EncounteredError += VlcPlayer_EncounteredError;
        }

        public void Cleanup()
        {
            _updateTimer?.Stop();
            if (_vlcPlayer != null)
            {
                _vlcPlayer.Playing -= VlcPlayer_Playing;
                _vlcPlayer.Paused -= VlcPlayer_Paused;
                _vlcPlayer.Stopped -= VlcPlayer_Stopped;
                _vlcPlayer.TimeChanged -= VlcPlayer_TimeChanged;
                _vlcPlayer.LengthChanged -= VlcPlayer_LengthChanged;
                _vlcPlayer.EndReached -= VlcPlayer_EndReached;
                _vlcPlayer.Opening -= VlcPlayer_Opening;
                _vlcPlayer.EncounteredError -= VlcPlayer_EncounteredError;

                _vlcPlayer.Stop();
                _vlcPlayer.Dispose();
            }
        }

        #endregion
    }
}