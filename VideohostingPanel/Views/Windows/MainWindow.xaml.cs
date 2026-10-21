using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using VideohostingPanel.Services;
using VideohostingPanel.ViewModels.Base;
using VideohostingPanel.ViewModels.WindowViewModels;

namespace VideohostingPanel.Views.Windows
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _mainViewModel;

        public MainWindow(MainViewModel mainViewModel)
        {
            InitializeComponent();
            _mainViewModel = mainViewModel;
            DataContext = _mainViewModel;

            _mainViewModel.ModalPageRequested += OnModalPageRequested;
            _mainViewModel.HideModalPageRequested += OnHideModalPageRequested;

            Closed += OnClosed;
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState != MouseButtonState.Pressed)
                return;

            try
            {
                DragMove();
            }
            catch (InvalidOperationException)
            {
                // Окно уже разворачивается/закрывается.
            }
        }

        /// <summary>
        /// Короткое описание содержимого фреймов главного окна. Вызывается только из UI-потока
        /// (DiagLog.CaptureStateUnsafe) — по нему видно, на какой странице залип интерфейс.
        /// </summary>
        public string DescribeFrames()
        {
            try
            {
                var main = MainFrame.Content as Page;
                return $"mainFrame={(main == null ? "null" : main.GetType().Name)}" +
                       $",mainFrameVis={MainFrame.Visibility},mainContentVis={MainContent.Visibility}";
            }
            catch (Exception ex)
            {
                return "DescribeFrames failed: " + ex.GetType().Name;
            }
        }

        /// <summary>
        /// Показывает или скрывает системные кнопки окна на модальных страницах.
        /// </summary>
        public void SetModalWindowControlsVisible(bool visible)
        {
            DiagLog.Mark("MainWindow.SetModalWindowControlsVisible", $"visible={visible} {DiagLog.DescribeState()}");
            ModalWindowButtons.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            DiagLog.Mark("MainWindow.CloseButton_Click", "закрытие приложения");
            ReleaseModalPage("CloseButton");
            System.Windows.Application.Current.Shutdown();
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            DiagLog.Mark("MainWindow.MinimizeButton_Click", $"state={WindowState}");
            WindowState = WindowState.Minimized;
        }

        private void MaximizeButton_Click(object sender, RoutedEventArgs e)
        {
            DiagLog.Mark("MainWindow.MaximizeButton_Click", $"state={WindowState}");
            WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
        }

        private void OnClosed(object? sender, EventArgs e)
        {
            DiagLog.Mark("MainWindow.OnClosed", "start");
            ReleaseModalPage("OnClosed");

            _mainViewModel.ModalPageRequested -= OnModalPageRequested;
            _mainViewModel.HideModalPageRequested -= OnHideModalPageRequested;
            _mainViewModel.Detach();
            DiagLog.Mark("MainWindow.OnClosed", "done");
        }

        private void OnModalPageRequested(BaseViewModel viewModel, Page page)
        {
            DiagLog.Mark("MainWindow.OnModalPageRequested", page.GetType().Name + " " + DiagLog.DescribeState());

            // Если поверх уже открыта другая модальная страница — сначала корректно её освобождаем.
            ReleaseModalPage("replaced by " + page.GetType().Name);

            MainFrame.Content = page;
            MainFrame.Visibility = Visibility.Visible;
            MainContent.Visibility = Visibility.Collapsed;
            SetModalWindowControlsVisible(true);

            DiagLog.Mark("MainWindow.OnModalPageRequested", "done");
        }

        private void OnHideModalPageRequested()
        {
            DiagLog.Mark("MainWindow.OnHideModalPageRequested", "start " + DiagLog.DescribeState());
            ReleaseModalPage("HideModalPageRequested");
            MainFrame.Visibility = Visibility.Collapsed;
            MainContent.Visibility = Visibility.Visible;
            SetModalWindowControlsVisible(false);
            DiagLog.Mark("MainWindow.OnHideModalPageRequested", "done");
        }

        /// <summary>
        /// Освобождает текущую модальную страницу. Критично для страницы проверки видео:
        /// без Cleanup() каждый открытый VlcControl оставался жив (утечка libvlc-инстансов).
        /// </summary>
        private void ReleaseModalPage(string reason)
        {
            var page = MainFrame.Content as Page;
            if (page == null)
            {
                MainFrame.Content = null;
                DiagLog.Mark("MainWindow.ReleaseModalPage", $"reason={reason}: страницы нет");
                return;
            }

            DiagLog.Mark("MainWindow.ReleaseModalPage", $"reason={reason} page={page.GetType().Name}");

            try
            {
                // Аварийный выход из полноэкранного режима до выгрузки страницы:
                // иначе Topmost/размеры окна остаются от предыдущей страницы.
                if (page is Views.Pages.VerifyVideo verifyVideo)
                {
                    verifyVideo.ForceExitFullScreen();
                    verifyVideo.Cleanup();
                }
            }
            catch (Exception ex)
            {
                DiagLog.Write("EXC", "ReleaseModalPage.Cleanup: " + ex);
            }
            finally
            {
                MainFrame.Content = null;
            }
        }
    }
}