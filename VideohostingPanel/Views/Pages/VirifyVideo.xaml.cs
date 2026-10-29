using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Input;
using System.Windows.Threading;
using VideohostingPanel.Services;
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
            DiagLog.Mark("View.OnLoaded", $"isFullScreen={_viewModel.IsFullScreen}");
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
            {
                DiagLog.Mark("View.OnLoaded", "VlcControl уже создан — повторная инициализация не нужна");
                return;
            }

            // Инициализация VLC только после того, как у хоста есть HWND и реальные размеры.
            // async void допустим: все исключения ловятся внутри InitializeVlcPlayerAsync.
            VideoHost.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(async () =>
            {
                var op = DiagLog.Begin("View.InitVlcDeferred");
                try
                {
                    DiagLog.Mark("View.OnLoaded", "deferred: before InitializeVlcPlayer");
                    await InitializeVlcPlayerAsync();
                    DiagLog.Mark("View.OnLoaded", "deferred: before LoadVideoCommand");

                    if (_vlcPlayer != null)
                        _viewModel.LoadVideoCommand.Execute(null);

                    DiagLog.Mark("View.OnLoaded", "deferred: LoadVideoCommand dispatched");
                }
                catch (Exception ex)
                {
                    DiagLog.Write("EXC", "View.InitVlcDeferred: " + ex);
                }
                finally
                {
                    DiagLog.End(op);
                }
            }));
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            DiagLog.Mark("View.OnUnloaded", $"isFullScreen={_viewModel.IsFullScreen} vlc{( _vlcPlayer != null ? "!=null" : "==null")}");

            if (_viewModel.IsFullScreen)
                _viewModel.IsFullScreen = false;

            if (_hostWindow != null)
            {
                _hostWindow.PreviewKeyDown -= OnWindowPreviewKeyDown;
                _viewModel.FullScreenChanged -= OnFullScreenChanged;
                _hostWindow = null;
            }

            _viewModel.OnGoBackRequested -= OnGoBackRequested;

            // ДИАГНОСТИКА: раньше страница выгружалась без освобождения VLC —
            // каждый VlcControl оставался жив (утечка инстансов libvlc, потоков и HWND).
            Cleanup();
        }

        private void OnGoBackRequested()
        {
            if (_viewModel.IsFullScreen)
                _viewModel.IsFullScreen = false;
        }

        /// <summary>
        /// Создаёт VlcControl и завершает его инициализацию (EndInit → libvlc_new).
        ///
        /// ГЛАВНОЕ: EndInit синхронно создаёт libvlc-инстанс. Первый вызов в процессе
        /// занимает СЕКУНДЫ (загрузка libvlc.dll + скан plugins, замер: 9.4 с холодный / 0.15 с тёплый).
        /// Поэтому перед EndInit обязательно ждём фоновый прогрев (VlcBootstrapper),
        /// иначе UI-поток блокируется и приложение выглядит зависшим.
        /// </summary>
        private async Task InitializeVlcPlayerAsync()
        {
            var op = DiagLog.Begin("View.InitializeVlcPlayer");
            try
            {
                DiagLog.Mark("View.InitializeVlcPlayer", "before new VlcControl");
                var player = new VlcControl();
                VlcControlTracker.OnCreated();

                DiagLog.Mark("View.InitializeVlcPlayer", "before BeginInit");
                player.BeginInit();
                player.Dock = DockStyle.Fill;

                // Настройки (VlcLibDirectory / VlcMediaplayerOptions) должны быть заданы
                // ДО EndInit — иначе Vlc.DotNet бросает InvalidOperationException.
                _viewModel.InitializeVlcPlayer(player);

                VideoHost.Child = player;
                _vlcPlayer = player;

                // Ждём прогрев libvlc, чтобы EndInit не блокировал UI на секунды.
                var warm = await WaitForWarmUpAsync();
                DiagLog.Mark("View.InitializeVlcPlayer",
                    $"warmup ok={warm.Ok} elapsed={warm.ElapsedMs}ms error={warm.Error}");

                // Пока ждали прогрев, пользователь мог уйти со страницы.
                if (!IsLoaded || _vlcPlayer != player)
                {
                    DiagLog.Mark("View.InitializeVlcPlayer", "страница уже выгружена — отменяем инициализацию");
                    SafeDisposeUninitialized(player);
                    return;
                }

                if (!warm.Ok)
                    ShowSlowInitHint();

                DiagLog.Mark("View.InitializeVlcPlayer", "before EndInit (создание libvlc-инстанса)");
                var sw = Stopwatch.StartNew();
                player.EndInit();
                sw.Stop();
                DiagLog.Mark("View.InitializeVlcPlayer", $"after EndInit за {sw.ElapsedMilliseconds} мс");

                if (!warm.Ok)
                    HideSlowInitHint();

                // VLC по умолчанию перехватывает клавиатуру: отключаем, чтобы F11/Space/Esc
                // доходили до окна приложения.
                try
                {
                    player.Video.IsKeyInputEnabled = false;
                    player.Video.IsMouseInputEnabled = false;
                    DiagLog.Mark("View.InitializeVlcPlayer", "input disabled on video");
                }
                catch (Exception ex)
                {
                    // Старые версии libvlc могут не поддерживать эти опции.
                    DiagLog.Write("VLC", "IsKeyInputEnabled/IsMouseInputEnabled не применились: " + ex.GetType().Name);
                }

                player.MouseClick += OnVlcMouseClick;
                player.KeyDown += OnVlcKeyDown;

                DiagLog.Mark("View.InitializeVlcPlayer", "done");
            }
            catch (Exception ex)
            {
                DiagLog.Write("EXC", "View.InitializeVlcPlayer: " + ex);
                HideSlowInitHint();
                ShowVlcInitError(ex);
            }
            finally
            {
                DiagLog.End(op);
            }
        }

        /// <summary>
        /// Ждёт прогрев libvlc, но не дольше таймаута. Возвращает false, если прогрев
        /// не удался/не успел: тогда EndInit сам создаст инстанс (медленно, но с
        /// индикатором загрузки, а не с «зависшим» окном).
        /// </summary>
        private static async Task<VlcBootstrapper.WarmUpResult> WaitForWarmUpAsync()
        {
            var warmTask = VlcBootstrapper.WarmUpAsync();
            // 15 с хватает с запасом (холодный libvlc_new на этой машине ~0.7 с).
            // Больше ждать нет смысла: страница уже открыта, просто без картинки.
            var timeout = TimeSpan.FromSeconds(15);

            var finished = await Task.WhenAny(warmTask, Task.Delay(timeout)).ConfigureAwait(true);
            if (finished != warmTask)
            {
                DiagLog.Write("VLC", $"прогрев не завершился за {timeout.TotalSeconds:0} с — продолжаем без него");
                return new VlcBootstrapper.WarmUpResult(false, -1, "timeout");
            }

            return await warmTask.ConfigureAwait(true);
        }

        /// <summary>
        /// Если прогрев не удался, EndInit выполнится на UI-потоке и снова заплатит
        /// за загрузку libvlc. В этом случае заранее показываем «Загрузка видео...»,
        /// чтобы пользователь видел причину паузы, а не думал, что приложение встало.
        /// </summary>
        private void ShowSlowInitHint()
        {
            try
            {
                _viewModel.StatusMessage = "Загрузка плеера...";
                _viewModel.StatusColor = "White";
                _viewModel.IsVideoLoading = true;
            }
            catch
            {
                // Страница могла быть уже выгружена.
            }
        }

        /// <summary>Убирает подсказку «Загрузка плеера...», если она была показана.</summary>
        private void HideSlowInitHint()
        {
            try
            {
                _viewModel.IsVideoLoading = false;
            }
            catch
            {
                // Страница могла быть уже выгружена.
            }
        }

        /// <summary>Освобождает контрол, у которого не вызывался EndInit.</summary>
        private static void SafeDisposeUninitialized(VlcControl player)
        {
            try
            {
                player.Dispose();
                VlcControlTracker.OnDisposed();
            }
            catch (Exception ex)
            {
                DiagLog.Write("VLC", "SafeDisposeUninitialized: " + ex.GetType().Name);
            }
        }

        private void ShowVlcInitError(Exception ex)
        {
            try
            {
                _viewModel.StatusMessage = "Не удалось инициализировать видеоплеер: " + ex.Message;
                _viewModel.StatusColor = "#FF4444";
                _viewModel.IsVideoLoading = false;
            }
            catch
            {
                // Страница могла быть уже выгружена.
            }
        }

        private void OnVlcMouseClick(object? sender, System.Windows.Forms.MouseEventArgs e)
        {
            // ДИАГНОСТИКА: раньше здесь вызывался _vlcPlayer.Focus() — фокус уходил
            // в WinForms-контрол, и WPF PreviewKeyDown (Esc/F11) переставал получать клавиши.
            // Теперь фокус остаётся в WPF.
            DiagLog.Mark("View.VlcMouseClick", $"button={e.Button} focusedElement={Keyboard.FocusedElement?.GetType().Name}");
            FocusWpfHost();

            if (e.Button == MouseButtons.Left)
                _viewModel.PlayPause();
        }

        /// <summary>
        /// Возвращает клавиатурный фокус в WPF, чтобы Esc/F11/Space гарантированно
        /// доходили до PreviewKeyDown окна, даже если клик ушёл в VLC (WinForms HWND).
        /// </summary>
        private void FocusWpfHost()
        {
            try
            {
                Focus();
                Keyboard.Focus(this);
            }
            catch (Exception ex)
            {
                DiagLog.Write("DIAG", "FocusWpfHost: " + ex.GetType().Name);
            }
        }

        private void OnVlcKeyDown(object? sender, System.Windows.Forms.KeyEventArgs e)
        {
            // ДИАГНОСТИКА: путь WinForms (клавиша пришла в VLC-контрол, а не в WPF).
            DiagLog.Mark("Key.WinForms", $"key={e.KeyCode} fullscreen={_viewModel.IsFullScreen}");
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
            // ДИАГНОСТИКА: путь WPF (клавиша пришла в окно).
            DiagLog.Mark("Key.WPF", $"key={e.Key} fullscreen={_viewModel.IsFullScreen}");
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
            DiagLog.Mark("FS.Enter", "start " + DiagLog.DescribeState());

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
            ActionButtonsPanel.Visibility = Visibility.Collapsed;

            // Кнопки окна модальной страницы прячем: поверх WindowsFormsHost (airspace)
            // они всё равно не видны, а в полном экране не нужны.
            // Выход из полного экрана — Esc или F11 (см. RegisterAppHotKeys).
            if (_hostWindow is MainWindow mainWindow)
            {
                try
                {
                    mainWindow.SetModalWindowControlsVisible(false);
                }
                catch (Exception ex)
                {
                    DiagLog.Write("FS", "SetModalWindowControlsVisible(false): " + ex.GetType().Name);
                }
            }

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

            // Подписки на SizeChanged ОБЯЗАТЕЛЬНО до установки границ окна:
            // WPF применяет новые Width/Height синхронно внутри сеттера, поэтому
            // подписка «после» пропускает единственное событие изменения размера.
            if (_hostWindow != null)
            {
                _hostWindow.SizeChanged -= OnHostWindowSizeChanged;
                _hostWindow.SizeChanged += OnHostWindowSizeChanged;
            }

            RootGrid.SizeChanged -= OnHostWindowSizeChanged;
            RootGrid.SizeChanged += OnHostWindowSizeChanged;

            FormScroll.SizeChanged -= OnHostWindowSizeChanged;
            FormScroll.SizeChanged += OnHostWindowSizeChanged;

            if (_hostWindow != null)
            {
                _prevWindowState = _hostWindow.WindowState;
                _prevResizeMode = _hostWindow.ResizeMode;
                _prevTopmost = _hostWindow.Topmost;
                _prevBounds = new Rect(_hostWindow.Left, _hostWindow.Top, _hostWindow.Width, _hostWindow.Height);

                // Разворачиваем на рабочий стол (БЕЗ Topmost и БЕЗ перекрытия панели задач):
                // раньше Topmost=true поверх всех окон был второй причиной «приложение не закрыть»,
                // потому что поверх висело видео, а кнопки окна были спрятаны.
                var handle = new WindowInteropHelper(_hostWindow).Handle;
                if (handle == IntPtr.Zero)
                {
                    DiagLog.Write("FS", "EnterFullScreen: HWND ещё не создан — используем основной экран");
                }

                var screen = handle == IntPtr.Zero
                    ? System.Windows.Forms.Screen.PrimaryScreen
                    : System.Windows.Forms.Screen.FromHandle(handle);

                var dpi = VisualTreeHelper.GetDpi(this);
                var work = screen?.WorkingArea ?? new System.Drawing.Rectangle(0, 0,
                    (int)SystemParameters.PrimaryScreenWidth, (int)SystemParameters.PrimaryScreenHeight);

                _hostWindow.WindowState = WindowState.Normal;
                _hostWindow.ResizeMode = ResizeMode.NoResize;
                _hostWindow.Topmost = false;
                _hostWindow.Left = work.Left / dpi.DpiScaleX;
                _hostWindow.Top = work.Top / dpi.DpiScaleY;
                _hostWindow.Width = work.Width / dpi.DpiScaleX;
                _hostWindow.Height = work.Height / dpi.DpiScaleY;

                DiagLog.Mark("FS.Enter", "после установки границ");
            }

            // Горячие клавиши уровня окна: даже если фокус ушёл в VLC (WinForms HWND),
            // Esc/F11 продолжают работать.
            RegisterAppHotKeys(true);

            // Страховка: пока страница жива, Topmost обязан быть false.
            StartFullScreenGuard();

            // Пересчёт после того, как окно реально получило новые размеры:
            // SizeChanged при программной установке Width/Height может прийти раньше подписки.
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(ResizeVideoToWindow));

            DiagLog.Mark("FS.Enter", "done " + DiagLog.DescribeState());
        }

        private void ExitFullScreen()
        {
            _fullScreenApplied = false;
            DiagLog.Mark("FS.Exit", "start " + DiagLog.DescribeState());

            StopFullScreenGuard();
            RegisterAppHotKeys(false);

            BackButtonPanel.Visibility = Visibility.Visible;
            FormHeader.Visibility = Visibility.Visible;
            VideoInfoPanel.Visibility = Visibility.Visible;
            ExtraInfoPanel.Visibility = Visibility.Visible;
            ActionButtonsPanel.Visibility = Visibility.Visible;

            if (_hostWindow is MainWindow mainWindow)
            {
                try
                {
                    mainWindow.SetModalWindowControlsVisible(true);
                }
                catch (Exception ex)
                {
                    DiagLog.Write("FS", "SetModalWindowControlsVisible(true): " + ex.GetType().Name);
                }
            }

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
            FormScroll.SizeChanged -= OnHostWindowSizeChanged;

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

            DiagLog.Mark("FS.Exit", "done " + DiagLog.DescribeState());
        }

        #region Горячие клавиши и страховка полного экрана

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private const int HotKeyEscape = 0xB017;
        private const int HotKeyF11 = 0xB018;
        private const uint VK_ESCAPE = 0x1B;
        private const uint VK_F11 = 0x7A;

        private HwndSource? _hotKeySource;
        private bool _hotKeysRegistered;
        private DispatcherTimer? _fullScreenGuard;
        private bool _exitingFullScreenInternally;

        private void RegisterAppHotKeys(bool register)
        {
            try
            {
                if (register)
                {
                    if (_hotKeysRegistered || _hostWindow == null)
                        return;

                    var handle = new WindowInteropHelper(_hostWindow).Handle;
                    if (handle == IntPtr.Zero)
                    {
                        DiagLog.Write("FS", "RegisterHotKey: HWND == 0 — пропускаем");
                        return;
                    }

                    _hotKeySource = HwndSource.FromHwnd(handle);
                    if (_hotKeySource == null)
                        return;

                    _hotKeySource.AddHook(HotKeyHook);

                    var esc = RegisterHotKey(handle, HotKeyEscape, 0, VK_ESCAPE);
                    var f11 = RegisterHotKey(handle, HotKeyF11, 0, VK_F11);
                    _hotKeysRegistered = esc || f11;

                    DiagLog.Write("FS", $"RegisterHotKey esc={esc} f11={f11}");
                    return;
                }

                if (!_hotKeysRegistered || _hostWindow == null)
                    return;

                var hwnd = new WindowInteropHelper(_hostWindow).Handle;
                UnregisterHotKey(hwnd, HotKeyEscape);
                UnregisterHotKey(hwnd, HotKeyF11);
                _hotKeySource?.RemoveHook(HotKeyHook);
                _hotKeySource = null;
                _hotKeysRegistered = false;
                DiagLog.Write("FS", "UnregisterHotKey done");
            }
            catch (Exception ex)
            {
                DiagLog.Write("FS", "RegisterAppHotKeys: " + ex);
            }
        }

        private IntPtr HotKeyHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_HOTKEY = 0x0312;
            if (msg != WM_HOTKEY)
                return IntPtr.Zero;

            var id = wParam.ToInt32();
            DiagLog.Mark("FS.HotKey", $"id=0x{id:X}");

            if (id == HotKeyEscape)
            {
                if (_viewModel.IsFullScreen)
                {
                    _viewModel.IsFullScreen = false;
                    handled = true;
                }
            }
            else if (id == HotKeyF11)
            {
                _viewModel.ToggleFullScreenCommand.Execute(null);
                handled = true;
            }

            return IntPtr.Zero;
        }

        /// <summary>
        /// Пока страница в полноэкранном режиме, раз в 500 мс возвращаем окну нормальные
        /// границы, если Topmost включён или окно ушло за пределы рабочего стола.
        /// Это последняя страховка от «приложение висит поверх всего и его не закрыть».
        /// </summary>
        private void StartFullScreenGuard()
        {
            _fullScreenGuard?.Stop();

            _fullScreenGuard = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromMilliseconds(500)
            };

            _fullScreenGuard.Tick += (_, _) => EnforceFullScreenSafety();
            _fullScreenGuard.Start();
        }

        private void StopFullScreenGuard()
        {
            _fullScreenGuard?.Stop();
            _fullScreenGuard = null;
        }

        private void EnforceFullScreenSafety()
        {
            if (_hostWindow == null)
                return;

            try
            {
                if (_hostWindow.Topmost && !_exitingFullScreenInternally)
                {
                    DiagLog.Write("FS", "GUARD: Topmost неожиданно true — сбрасываем");
                    _hostWindow.Topmost = false;
                }

                if (!_viewModel.IsFullScreen)
                    StopFullScreenGuard();
            }
            catch (Exception ex)
            {
                DiagLog.Write("FS", "GUARD error: " + ex.GetType().Name);
            }
        }

        #endregion

        /// <summary>
        /// Аварийный сброс полноэкранного режима без обращения к ViewModel.
        /// Используется, когда дальше оставаться в fullscreen нельзя (например, при аварийном закрытии).
        /// </summary>
        public void ForceExitFullScreen()
        {
            DiagLog.Mark("FS.ForceExit", "вызван аварийный сброс");
            _exitingFullScreenInternally = true;
            try
            {
                if (_fullScreenApplied)
                {
                    ExitFullScreen();
                }
                else
                {
                    // FullScreen не применялся, но страховки могли остаться.
                    RegisterAppHotKeys(false);
                    StopFullScreenGuard();

                    if (_hostWindow != null)
                    {
                        _hostWindow.Topmost = false;
                        _hostWindow.ResizeMode = ResizeMode.CanResize;
                    }
                }
            }
            catch (Exception ex)
            {
                DiagLog.Write("FS", "ForceExitFullScreen: " + ex);
            }
            finally
            {
                _exitingFullScreenInternally = false;
            }
        }

        private void OnHostWindowSizeChanged(object? sender, SizeChangedEventArgs e)
        {
            if (!_fullScreenApplied)
                return;

            DiagLog.Mark("FS.SizeChanged", $"root={RootGrid.ActualHeight:0} viewport={FormScroll.ActualHeight:0} video={VideoContainer.Height:0}");
            ResizeVideoToWindow();
        }

        private void ResizeVideoToWindow()
        {
            if (!_fullScreenApplied)
                return;

            // Доступная высота — это вьюпорт FormScroll. В полноэкранном режиме
            // панелей под видео нет, поэтому видео занимает вьюпорт целиком.
            var available = FormScroll.ActualHeight;
            if (available <= 0)
                available = FormScroll.ViewportHeight;
            if (available <= 0)
                return;

            // Защита от лишних проходов раскладки: высота уже правильная.
            if (Math.Abs(VideoContainer.Height - available) < 0.5)
                return;

            DiagLog.Mark("FS.Resize", $"viewport={available:0} root={RootGrid.ActualHeight:0} video={VideoContainer.Height:0}");
            VideoContainer.Height = available;
        }

        #endregion

        #region Перемотка

        private bool _thumbDrag;

        private void VideoSlider_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left)
                return;

            var slider = (Slider)sender;

            // Клик по самому бегунку — это начало перетаскивания, позицию не меняем.
            if (FindAncestor<Thumb>(e.OriginalSource as DependencyObject) != null)
            {
                _viewModel.IsDragging = true;
                _thumbDrag = true;
                return;
            }

            // Одиночный клик по полосе: сразу перематываем в точку клика.
            // Перетаскивания тут нет, поэтому IsDragging не поднимаем: иначе при
            // потере MouseUp (слайдер больше не захватывает мышь) бегунок замрёт.
            _thumbDrag = false;
            _viewModel.IsDragging = false;

            // Считаем долю по геометрии дорожки, а не по ActualWidth слайдера:
            // дорожка уже ширины слайдера на ширину бегунка (12 px), и по краям
            // ошибка была заметной. Центр бегунка ходит от thumb/2 до width-thumb/2.
            var track = FindTrack(slider);
            var trackWidth = track?.ActualWidth ?? slider.ActualWidth;
            if (trackWidth <= 0)
                return;

            var x = track != null
                ? e.GetPosition(track).X
                : e.GetPosition(slider).X;

            var thumbWidth = track?.Thumb?.ActualWidth ?? 0;
            var usable = trackWidth - thumbWidth;
            var ratio = usable > 0
                ? (x - thumbWidth / 2) / usable
                : x / trackWidth;

            ratio = Math.Clamp(ratio, 0d, 1d);

            // Гасим встроенную обработку клика по дорожке (LargeChange / move-to-point):
            // иначе Slider сам двигает Value, и это спорит с нашей перемоткой.
            e.Handled = true;

            // Значение слайдера не выставляем напрямую: оно привязано к
            // VideoSliderPosition, и SeekVideo обновит бегунок через биндинг.
            _viewModel.SeekVideo(ratio);
        }

        /// <summary>PART_Track из шаблона слайдера (нужен для точного расчёта доли).</summary>
        private static Track? FindTrack(Slider slider)
        {
            slider.ApplyTemplate();
            return slider.Template?.FindName("PART_Track", slider) as Track;
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
            var op = DiagLog.Begin("View.Cleanup", $"vlc={(_vlcPlayer != null)}");
            try
            {
                DiagLog.Mark("View.Cleanup", "before ForceExitFullScreen");
                ForceExitFullScreen();
                DiagLog.Mark("View.Cleanup", "before ViewModel.Cleanup");

                _viewModel?.Cleanup();

                if (_vlcPlayer != null)
                {
                    if (_vlcPlayer.IsDisposed == false)
                    {
                        try
                        {
                            DiagLog.Mark("View.Cleanup", "before unsubscribe VlcControl events");
                            _vlcPlayer.MouseClick -= OnVlcMouseClick;
                            _vlcPlayer.KeyDown -= OnVlcKeyDown;
                            VlcControlTracker.OnDisposed();
                        }
                        catch (Exception ex)
                        {
                            DiagLog.Write("VLC", "View.Cleanup unsubscribe: " + ex.GetType().Name);
                        }
                    }

                    _vlcPlayer = null;
                }

                DiagLog.Mark("View.Cleanup", "done");
            }
            finally
            {
                DiagLog.End(op);
            }
        }

        private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            // Событие может прийти во время InitializeComponent, когда _viewModel ещё не задан.
            _viewModel?.VolumeChanged(e.NewValue);
        }
    }
}
