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

            MouseLeftButtonDown += (s, e) =>
            {
                if (e.ButtonState == MouseButtonState.Pressed)
                    DragMove();
            };
        }

        private void OnModalPageRequested(BaseViewModel viewModel, Page page)
        {
            MainFrame.Content = page;
            MainFrame.Visibility = Visibility.Visible;
        }

        private void OnHideModalPageRequested()
        {
            MainFrame.Content = null;
            MainFrame.Visibility = Visibility.Collapsed;
        }
    }
}