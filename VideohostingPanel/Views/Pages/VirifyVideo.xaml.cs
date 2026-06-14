using System.Windows.Controls;
using System.Windows.Forms.Integration;
using System.Windows.Input;
using VideohostingPanel.ViewModels.VerifyViewModels;
using Vlc.DotNet.Forms;

namespace VideohostingPanel.Views.Pages
{
    public partial class VerifyVideo : Page
    {
        private VlcControl _vlcPlayer;
        private VerifyVideoViewModel _viewModel;

        public VerifyVideo(VerifyVideoViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            DataContext = viewModel;
            InitializeVlcPlayer();
        }

        private void InitializeVlcPlayer()
        {
            _vlcPlayer = new VlcControl();
            _vlcPlayer.Dock = DockStyle.Fill;
            VideoHost.Child = _vlcPlayer;
            _viewModel.InitializeVlcPlayer(_vlcPlayer);
        }

        private void VideoSlider_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            _viewModel.IsDragging = true;
        }

        private void VideoSlider_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            _viewModel.IsDragging = false;
            var slider = (Slider)sender;
            _viewModel.SeekVideo(slider.Value);
        }

        public void Cleanup()
        {
            _viewModel?.Cleanup();
        }
    }
}