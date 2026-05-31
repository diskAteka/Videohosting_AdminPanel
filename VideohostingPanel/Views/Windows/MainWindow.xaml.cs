using System.Windows;
using System.Windows.Input;
using VideohostingPanel.Interfaces;
using VideohostingPanel.ViewModels;

namespace VideohostingPanel.Views.Windows
{
    public partial class MainWindow : Window, IService
    {
        private readonly IServiceProvider _serviceProvider;
        public MainWindow(MainViewModel mainViewModel, IServiceProvider serviceProvider)
        {
            InitializeComponent();
            DataContext = mainViewModel;
            _serviceProvider = serviceProvider;
        }
    }
}