using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Input;
using VideohostingPanel.Interfaces;

namespace VideohostingPanel.Views.Windows
{
    public partial class LoginWindow : Window, IService
    {
        private readonly IServiceProvider _serviceProvider;

        public LoginWindow(IServiceProvider serviceProvider)
        {
            InitializeComponent();
            _serviceProvider = serviceProvider;
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                this.DragMove();
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            string username = LoginTextBox.Text;
            string password = PasswordBox.Password;

            if (username == "admin" && password == "admin")
            {
                var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
                mainWindow.Show();
                this.Close();
            }
            else
            {
                ErrorMessageBorder.Visibility = Visibility.Visible;
                var storyboard = (System.Windows.Media.Animation.Storyboard)this.Resources["ShowErrorAnimation"];
                if (storyboard != null)
                {
                    storyboard.Begin(ErrorMessageBorder);
                }
            }
        }
    }
}