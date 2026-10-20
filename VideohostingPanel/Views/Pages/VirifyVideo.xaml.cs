using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Input;
using System.Windows.Threading;
using VideohostingPanel.ViewModels.VerifyViewModels;
using VideohostingPanel.Views.Windows;
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

            // VLC по умолчанию перехватывает клавиатуру: отключаем, чтобы F11/Space/Esc
            // доходили до окна приложения.
            try
            {
                _vlcPlayer.Video.IsKeyInputEnabled = false;
                _vlcPlayer.Video.IsMouseInputEnabled = false;
            }
            catch
            {
                // Старые версии libvlc могут не поддерживать эти опции.
            }

            _vlcPlayer.MouseClick += (s, e) =>
            {
                // Клик по видео забирает фокус клавиатуры на контрол VLC,
                // чтобы дальше работали F11 / Space / Esc.
                _vlcPlayer.Focus();

                if (e.Button == MouseButtons.Left)
                    _viewModel.PlayPause();
            };
            _vlcPlayer.KeyDown += OnVlcKeyDown;
        }

        private void OnVlcKeyDown(object? sender, System.Windows.Forms.KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case System.Windows.Forms.Keys.F11:
                    _viewModel.ToggleFullScreenCommand.Execute(null);
                    e.Handled = true;
                    break;
                case System.Windows.Forms.Keys.Space:
                    _viewModel.PlayPause();
                    e.Handled = true;
                    break;
                case System.Windows.Forms.Keys.Escape:
                    if (_viewModel.IsFullScreen)
                    {
                        _viewModel.IsFullScreen = false;
                        e.Handled = true;
                    }
                    break;
            }
        }

        private void OnWindowPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.F11)
            {
                _viewModel.ToggleFullScreenCommand.Execute(null);
                e.Handled = true;
            }
            else if (e.Key == Key.Space)
            {
                if (Keyboard.FocusedElement is System.Windows.Controls.Primitives.TextBoxBase)
                    return;

                _viewModel.PlayPause();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape && _viewModel.IsFullScreen)
            {
                _viewModel.IsFullScreen = false;
                e.Handled = true;
            }
        }

        #region Полноэкранный режим

        private bool _fullScreenApplied;
        private WindowState _prevWindowState;
        private ResizeMode _prevResizeMode;
        private bool _prevTopmost;
        private Rect _prevBounds;

        private Thickness _prevContentGridMargin;
        private Thickness _prevFormStackMargin;
        private Thickness _prevVideoMargin;
        private double _prevVideoHeight;
        private double _prevVideoMinHeight;
        private CornerRadius _prevFormCornerRadius;
        private CornerRadius _prevVideoCornerRadius;
        private CornerRadius _prevControlsCornerRadius;
        private Thickness _prevFormBorderThickness;
        private Thickness _prevVideoBorderThickness;

        private void OnFullScreenChanged(bool isFullScreen)
        {
            if (isFullScreen == _fullScreenApplied)
                return;

            if (isFullScreen)
                EnterFullScreen();
            else
                ExitFullScreen();
        }

        private void EnterFullScreen()
        {
            _fullScreenApplied = true;

            // Запоминаем исходное оформление
            _prevContentGridMargin = ContentGrid.Margin;
            _prevFormStackMargin = FormStack.Margin;
            _prevVideoMargin = VideoContainer.Margin;
            _prevVideoHeight = VideoContainer.Height;
            _prevVideoMinHeight = VideoContainer.MinHeight;
            _prevFormCornerRadius = FormBorder.CornerRadius;
            _prevVideoCornerRadius = VideoContainer.CornerRadius;
            _prevControlsCornerRadius = ControlsBorder.CornerRadius;
            _prevFormBorderThickness = FormBorder.BorderThickness;
            _prevVideoBorderThickness = VideoContainer.BorderThickness;

            // Прячем всё, кроме видео
            BackButtonPanel.Visibility = Visibility.Collapsed;
            FormHeader.Visibility = Visibility.Collapsed;
            VideoInfoPanel.Visibility = Visibility.Collapsed;
            ExtraInfoPanel.Visibility = Visibility.Collapsed;
            RejectCommentBorder.Visibility = Visibility.Collapsed;
            StatusText.Visibility = Visibility.Collapsed;
            ActionButtonsPanel.Visibility = Visibility.Collapsed;
            FullScreenWindowButtons.Visibility = Visibility.Visible;

            // Кнопки окна модальной страницы уступают место кнопкам полного экрана.
            if (_hostWindow is MainWindow mainWindow)
                mainWindow.SetModalWindowControlsVisible(false);

            ContentGrid.Margin = new Thickness(0);
            FormStack.Margin = new Thickness(0);
            FormScroll.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
            FormScroll.ScrollToTop();

            FormBorder.CornerRadius = new CornerRadius(0);
            FormBorder.BorderThickness = new Thickness(0);
            VideoContainer.CornerRadius = new CornerRadius(0);
            VideoContainer.BorderThickness = new Thickness(0);
            VideoContainer.Margin = new Thickness(0);
            VideoContainer.MinHeight = 0;
            ControlsBorder.CornerRadius = new CornerRadius(0);

            ResizeVideoToWindow();

            if (_hostWindow != null)
            {
                _prevWindowState = _hostWindow.WindowState;
                _prevResizeMode = _hostWindow.ResizeMode;
                _prevTopmost = _hostWindow.Topmost;
                _prevBounds = new Rect(_hostWindow.Left, _hostWindow.Top, _hostWindow.Width, _hostWindow.Height);

                // Разворачиваем на весь экран (включая область панели задач)
                var handle = new WindowInteropHelper(_hostWindow).Handle;
                var screen = System.Windows.Forms.Screen.FromHandle(handle);
                var dpi = VisualTreeHelper.GetDpi(this);

                _hostWindow.WindowState = WindowState.Normal;
                _hostWindow.ResizeMode = ResizeMode.NoResize;
                _hostWindow.Topmost = true;
                _hostWindow.Left = screen.Bounds.Left / dpi.DpiScaleX;
                _hostWindow.Top = screen.Bounds.Top / dpi.DpiScaleY;
                _hostWindow.Width = screen.Bounds.Width / dpi.DpiScaleX;
                _hostWindow.Height = screen.Bounds.Height / dpi.DpiScaleY;

                _hostWindow.SizeChanged -= OnHostWindowSizeChanged;
                _hostWindow.SizeChanged += OnHostWindowSizeChanged;
            }

            RootGrid.SizeChanged -= OnHostWindowSizeChanged;
            RootGrid.SizeChanged += OnHostWindowSizeChanged;
        }

        private void ExitFullScreen()
        {
            _fullScreenApplied = false;

            BackButtonPanel.Visibility = Visibility.Visible;
            FormHeader.Visibility = Visibility.Visible;
            VideoInfoPanel.Visibility = Visibility.Visible;
            ExtraInfoPanel.Visibility = Visibility.Visible;
            RejectCommentBorder.Visibility = Visibility.Visible;
            StatusText.Visibility = Visibility.Visible;
            ActionButtonsPanel.Visibility = Visibility.Visible;
            FullScreenWindowButtons.Visibility = Visibility.Collapsed;

            if (_hostWindow is MainWindow mainWindow)
                mainWindow.SetModalWindowControlsVisible(true);

            ContentGrid.Margin = _prevContentGridMargin;
            FormStack.Margin = _prevFormStackMargin;
            FormScroll.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;

            FormBorder.CornerRadius = _prevFormCornerRadius;
            FormBorder.BorderThickness = _prevFormBorderThickness;
            VideoContainer.CornerRadius = _prevVideoCornerRadius;
            VideoContainer.BorderThickness = _prevVideoBorderThickness;
            VideoContainer.Margin = _prevVideoMargin;
            VideoContainer.Height = _prevVideoHeight;
            VideoContainer.MinHeight = _prevVideoMinHeight;
            ControlsBorder.CornerRadius = _prevControlsCornerRadius;

            RootGrid.SizeChanged -= OnHostWindowSizeChanged;

            if (_hostWindow != null)
            {
                _hostWindow.SizeChanged -= OnHostWindowSizeChanged;
                _hostWindow.Topmost = _prevTopmost;
                _hostWindow.ResizeMode = _prevResizeMode;
                _hostWindow.WindowState = WindowState.Normal;
                _hostWindow.Left = _prevBounds.Left;
                _hostWindow.Top = _prevBounds.Top;
                _hostWindow.Width = _prevBounds.Width;
                _hostWindow.Height = _prevBounds.Height;
                _hostWindow.WindowState = _prevWindowState;
            }
        }

        private void OnHostWindowSizeChanged(object? sender, SizeChangedEventArgs e)
        {
            if (_fullScreenApplied)
                ResizeVideoToWindow();
        }

        private void ResizeVideoToWindow()
        {
            var height = RootGrid.ActualHeight;
            if (height > 0)
                VideoContainer.Height = height;
        }

        private void FullScreenMinimize_Click(object sender, RoutedEventArgs e)
        {
            if (_hostWindow != null)
                _hostWindow.WindowState = WindowState.Minimized;
        }

        private void FullScreenRestore_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.IsFullScreen = false;
        }

        private void FullScreenClose_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Application.Current.Shutdown();
        }

        #endregion

        #region Перемотка

        private bool _thumbDrag;

        private void VideoSlider_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left)
                return;

            var slider = (Slider)sender;
            _viewModel.IsDragging = true;

            // Клик по самому бегунку — это начало перетаскивания, позицию не меняем.
            if (FindAncestor<Thumb>(e.OriginalSource as DependencyObject) != null)
            {
                _thumbDrag = true;
                return;
            }

            // Одиночный клик по полосе: сразу перематываем в точку клика.
            _thumbDrag = false;

            var width = slider.ActualWidth;
            if (width <= 0)
                return;

            var ratio = e.GetPosition(slider).X / width;
            ratio = Math.Clamp(ratio, 0d, 1d);

            // Значение слайдера не выставляем напрямую: оно привязано к
            // VideoSliderPosition, и SeekVideo обновит бегунок через биндинг.
            _viewModel.SeekVideo(ratio);
        }

        private void VideoSlider_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left)
                return;

            var wasThumbDrag = _thumbDrag;
            _thumbDrag = false;
            _viewModel.IsDragging = false;

            // При перетаскивании бегунка перематываем по отпусканию.
            if (wasThumbDrag)
            {
                var slider = (Slider)sender;
                _viewModel.SeekVideo(slider.Value);
            }
        }

        private void VideoSlider_LostMouseCapture(object sender, System.Windows.Input.MouseEventArgs e)
        {
            // Страховка: если отпускание мыши пришло вне слайдера,
            // завершаем перетаскивание и снимаем флаг, иначе таймер
            // перестанет обновлять позицию.
            if (_thumbDrag && sender is Slider slider)
            {
                _thumbDrag = false;
                _viewModel.SeekVideo(slider.Value);
            }

            _thumbDrag = false;
            _viewModel.IsDragging = false;
        }

        private static T? FindAncestor<T>(DependencyObject? source) where T : DependencyObject
        {
            while (source != null)
            {
                if (source is T match)
                    return match;

                source = source is Visual or System.Windows.Media.Media3D.Visual3D
                    ? VisualTreeHelper.GetParent(source)
                    : LogicalTreeHelper.GetParent(source);
            }

            return null;
        }

        #endregion

        public void Cleanup()
        {
            _viewModel?.Cleanup();
        }

        private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            // Событие может прийти во время InitializeComponent, когда _viewModel ещё не задан.
            _viewModel?.VolumeChanged(e.NewValue);
        }
    }
}
