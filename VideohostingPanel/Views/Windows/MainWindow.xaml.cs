using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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
        /// Показывает или скрывает системные кнопки окна на модальных страницах.
        /// </summary>
        public void SetModalWindowControlsVisible(bool visible)
        {
            ModalWindowButtons.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            ReleaseModalPage();
            System.Windows.Application.Current.Shutdown();
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void MaximizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
        }

        private void OnClosed(object? sender, EventArgs e)
        {
            ReleaseModalPage();

            _mainViewModel.ModalPageRequested -= OnModalPageRequested;
            _mainViewModel.HideModalPageRequested -= OnHideModalPageRequested;
            _mainViewModel.Detach();
        }

        private void OnModalPageRequested(BaseViewModel viewModel, Page page)
        {
            // Если поверх уже открыта страница, сначала корректно освобождаем её.
            ReleaseModalPage();

            MainFrame.Content = page;
            MainFrame.Visibility = Visibility.Visible;
            MainContent.Visibility = Visibility.Collapsed;
            SetModalWindowControlsVisible(true);
        }

        private void OnHideModalPageRequested()
        {
            ReleaseModalPage();
            MainFrame.Visibility = Visibility.Collapsed;
            MainContent.Visibility = Visibility.Visible;
            SetModalWindowControlsVisible(false);
        }

        /// <summary>
        /// Освобождает текущую страницу: без Cleanup() каждый открытый VlcControl
        /// остаётся жив (утечка инстансов libvlc, потоков и HWND).
        /// </summary>
        private void ReleaseModalPage()
        {
            var page = MainFrame.Content as Page;
            if (page == null)
            {
                MainFrame.Content = null;
                return;
            }

            try
            {
                // Аварийный выход из полноэкранного режима до выгрузки страницы.
                if (page is Views.Pages.VerifyVideo verifyVideo)
                {
                    verifyVideo.ForceExitFullScreen();
                    verifyVideo.Cleanup();
                }
            }
            catch
            {
                // Страница уже могла быть выгружена.
            }

            MainFrame.Content = null;
        }
    }
}