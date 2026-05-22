using SharedLib.Models;
using System.Windows.Controls;

namespace VideohostingPanel.Pages
{
    public partial class EditVideo : Page
    {
        Video _video;
        public EditVideo(Video video)
        {
            InitializeComponent();
            _video = video;
        }

        private void DeleteVideo(object sender, System.Windows.RoutedEventArgs e)
        {

        }
    }
}
