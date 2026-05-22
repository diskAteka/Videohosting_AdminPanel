using SharedLib.Models;
using System.Windows.Controls;

namespace VideohostingPanel.Pages
{
    public partial class EditUser : Page
    {
        User _user;
        public EditUser(User user)
        {
            InitializeComponent();
            _user = user;
        }
    }
}
