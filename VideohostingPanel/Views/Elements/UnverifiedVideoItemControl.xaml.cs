using SharedLib.Contracts.Admin;
using System.Windows;
using VideohostingPanel.Services;
using VideohostingPanel.ViewModels.WindowViewModels;
using VideohostingPanel.Views.Windows;
using UserControl = System.Windows.Controls.UserControl;

namespace VideohostingPanel.Views.Elements
{
    public partial class UnverifiedVideoItemControl : UserControl
    {
        private GetVideoResponse? _video;

        public UnverifiedVideoItemControl()
        {
            InitializeComponent();
            Loaded += OnLoaded;
            DataContextChanged += OnDataContextChanged;
        }

        private void OnLoaded(object sender, RoutedEventArgs e) => LoadPoster();

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            _video = e.NewValue as GetVideoResponse;

            if (IsLoaded)
                LoadPoster();
        }

        private async void LoadPoster()
        {
            var video = _video;
            if (video == null)
                return;

            var poster = await PosterLoader.LoadAsync(video.Id);

            if (poster != null && ReferenceEquals(_video, video))
                PosterImage.Source = poster;
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
