using SharedLib.Models;
using System.Windows;
using VideohostingPanel.ViewModels.WindowViewModels;
using VideohostingPanel.Views.Windows;
using UserControl = System.Windows.Controls.UserControl;

namespace VideohostingPanel.Views.Elements
{
    public partial class EmployeeItemControl : UserControl
    {
        public EmployeeItemControl()
        {
            InitializeComponent();
        }

        private void Border_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (DataContext is Employee employee)
            {
                var mainVM = (Window.GetWindow(this) as MainWindow)?.DataContext as MainViewModel;
                mainVM?.OpenEditEmployeeWindow(employee);
            }
        }
    }
}
