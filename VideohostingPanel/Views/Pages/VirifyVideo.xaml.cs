using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms.Integration;
using System.Windows.Input;
using System.Windows.Threading;
using VideohostingPanel.ViewModels.VerifyViewModels;
using Vlc.DotNet.Forms;

namespace VideohostingPanel.Views.Pages
{
    public partial class VerifyVideo : Page
    {
        private VlcControl _vlcPlayer;
        private VerifyVideoViewModel _viewModel;
        private Window _hostWindow;

        public VerifyVideo(VerifyVideoViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            DataContext = viewModel;

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _hostWindow = Window.GetWindow(this);
            if (_hostWindow != null)
            {
                _hostWindow.PreviewKeyDown -= OnWindowPreviewKeyDown;
                _hostWindow.PreviewKeyDown += OnWindowPreviewKeyDown;
                _viewModel.FullScreenChanged -= OnFullScreenChanged;
                _viewModel.FullScreenChanged += OnFullScreenChanged;
            }

            _viewModel.OnGoBackRequested -= OnGoBackRequested;
            _viewModel.OnGoBackRequested += OnGoBackRequested;

            if (_vlcPlayer != null)
                return;

            // Инициализация VLC только после того, как у хоста есть HWND и реальные размеры.
            VideoHost.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
            {
                InitializeVlcPlayer();
                _viewModel.LoadVideoCommand.Execute(null);
            }));
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            if (_viewModel.IsFullScreen)
                _viewModel.IsFullScreen = false;

            if (_hostWindow != null)
            {
                _hostWindow.PreviewKeyDown -= OnWindowPreviewKeyDown;
                _viewModel.FullScreenChanged -= OnFullScreenChanged;
                _hostWindow = null;
            }

            _viewModel.OnGoBackRequested -= OnGoBackRequested;
        }

        private void OnGoBackRequested()
        {
            if (_viewModel.IsFullScreen)
                _viewModel.IsFullScreen = false;
        }

        private void InitializeVlcPlayer()
        {
            _vlcPlayer = new VlcControl();
            _vlcPlayer.BeginInit();
            _vlcPlayer.Dock = DockStyle.Fill;
            VideoHost.Child = _vlcPlayer;
            _viewModel.InitializeVlcPlayer(_vlcPlayer);
            _vlcPlayer.EndInit();
        }

        private void OnWindowPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.F11)
            {
                _viewModel.ToggleFullScreenCommand.Execute(null);
                e.Handled = true;
            }
            else if (e.Key == Key.Escape && _viewModel.IsFullScreen)
            {
                _viewModel.IsFullScreen = false;
                e.Handled = true;
            }
        }

        private void OnFullScreenChanged(bool isFullScreen)
        {
            if (_hostWindow == null)
                return;

            // Окно использует AllowsTransparency + WindowStyle=None, поэтому просто
            // переключаем состояние; стиль не меняем, иначе окно потеряет прозрачность.
            _hostWindow.WindowState = isFullScreen
                ? WindowState.Maximized
                : WindowState.Normal;
        }

        private void VideoSlider_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            _viewModel.IsDragging = true;
        }

        private void VideoSlider_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            _viewModel.IsDragging = false;
            var slider = (Slider)sender;
            _viewModel.SeekVideo(slider.Value);
        }

        public void Cleanup()
        {
            _viewModel?.Cleanup();
        }

        private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {

        }
    }
}
