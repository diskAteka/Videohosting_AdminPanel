using System.Windows;
using System.Windows.Input;
using VideohostingPanel.Interfaces;
using VideohostingPanel.ViewModels;

namespace VideohostingPanel.Views.Windows
{
    public partial class MainWindow : Window, IService
    {
        public MainWindow(MainViewModel mainViewModel)
        {
            InitializeComponent();
            DataContext = mainViewModel;
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                DragMove();
            }
            catch (InvalidOperationException)
            {
                // Игнорируем, если перетаскивание не удалось
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            bool isYes = MessageBox.Show("Вы уверены, что хотите выйти?", "Подтверждение выхода",
                MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
            if (isYes)
                Application.Current.Shutdown();
        }

        private void LogoutButton_Click(object sender, RoutedEventArgs e)
        {
            bool isYes = MessageBox.Show("Вы уверены, что хотите выйти из аккаунта?", "Подтверждение выхода",
                MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
            if (isYes)
            {
                // Здесь позже будем получать LoginWindow через DI
                LoginWindow loginWindow = new LoginWindow(App.Current as IServiceProvider ??
                    throw new InvalidOperationException());
                loginWindow.Show();
                this.Close();
            }
        }
    }
}