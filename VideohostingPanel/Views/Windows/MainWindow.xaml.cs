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
            _mainViewModel.ModalPageRequested -= OnModalPageRequested;
            _mainViewModel.HideModalPageRequested -= OnHideModalPageRequested;
            _mainViewModel.Detach();
        }

        private void OnModalPageRequested(BaseViewModel viewModel, Page page)
        {
            MainFrame.Content = page;
            MainFrame.Visibility = Visibility.Visible;
            MainContent.Visibility = Visibility.Collapsed;
            SetModalWindowControlsVisible(true);
        }

        private void OnHideModalPageRequested()
        {
            MainFrame.Content = null;
            MainFrame.Visibility = Visibility.Collapsed;
            MainContent.Visibility = Visibility.Visible;
            SetModalWindowControlsVisible(false);
        }
    }
}