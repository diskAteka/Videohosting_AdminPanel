using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Input;
using VideohostingPanel.Interfaces;

namespace VideohostingPanel.Views.Windows
{
    public partial class LoginWindow : Window, IService
    {
        public LoginWindow(IServiceProvider serviceProvider)
        {
            InitializeComponent();
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                this.DragMove();
        }
    }
}