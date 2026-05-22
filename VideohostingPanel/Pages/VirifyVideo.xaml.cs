using SharedLib.Models;
using System.Windows;
using System.Windows.Controls;

namespace VideohostingPanel.Pages
{
    public partial class VerifyVideo : Page
    {
        Video _video;
        public VerifyVideo(Video video)
        {
            InitializeComponent();
            _video = video;
        }

        private void ProgressSlider_ValueChanged(object sender, System.Windows.RoutedPropertyChangedEventArgs<double> e)
        {

        }

        private void PlayPauseButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {

        }

        private void ProgressSlider_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {

        }

        private void ProgressSlider_PreviewMouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {

        }

        private void MuteButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {

        }

        private void VolumeSlider_ValueChanged(object sender, System.Windows.RoutedPropertyChangedEventArgs<double> e)
        {

        }

        private void FullScreenButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {

        }

        private void Draw()
        {
            if (_video == null)
            {
                MessageBox.Show("Видео не найдено.");
                return;
            }
            string videoPath = _video.Link; // Путь к видеофайлу
            string Name = _video.Name;
            string Description = _video.Description;


        }

        private void VideoPlayer_MediaOpened(object sender, RoutedEventArgs e)
        {

        }

        private void VideoPlayer_MediaEnded(object sender, RoutedEventArgs e)
        {

        }

        private void VideoPlayer_MediaFailed(object sender, ExceptionRoutedEventArgs e)
        {

        }

        private void ApproveButton_Click(object sender, RoutedEventArgs e)
        {

        }

        private void RejectButton_Click(object sender, RoutedEventArgs e)
        {

        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}
