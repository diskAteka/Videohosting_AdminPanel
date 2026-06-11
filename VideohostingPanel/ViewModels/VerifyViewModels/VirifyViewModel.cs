using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using SharedLib.Contracts.Admin;
using SharedLib.Models;
using VideohostingPanel.Services;
using VideohostingPanel.ViewModels.Base;

namespace VideohostingPanel.ViewModels.VerifyViewModels
{
    public partial class VirifyViewModel : BaseViewModel
    {
        private readonly ApiService _apiService;

        #region

        [ObservableProperty]
        private BitmapImage? _posterImageSource;

        [ObservableProperty]
        private GetVideoResponse? _video;

        [ObservableProperty]
        private double _videoSliderPosition;

        [ObservableProperty]
        private double _volume;

        #endregion
    }
}
