using SharedLib.Contracts.Admin;
using System.Windows;
using VideohostingPanel.ViewModels.WindowViewModels;
using VideohostingPanel.Views.Windows;
using UserControl = System.Windows.Controls.UserControl;

namespace VideohostingPanel.Views.Elements
{
    public partial class UnverifiedVideoItemControl : UserControl
    {
        public UnverifiedVideoItemControl()
        {
            InitializeComponent();
        }

        private void Border_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (DataContext is GetVideoResponse video)
            {
                var mainVM = (Window.GetWindow(this) as MainWindow)?.DataContext as MainViewModel;
                mainVM?.OpenVerificationPage(video);
            }
        }
    }
}
