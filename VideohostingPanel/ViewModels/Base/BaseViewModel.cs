using CommunityToolkit.Mvvm.ComponentModel;

namespace VideohostingPanel.ViewModels.Base
{
    public partial class BaseViewModel : ObservableObject
    {
        [ObservableProperty]
        private bool _isBusy;

        [ObservableProperty]
        private string _title = string.Empty;
    }
}
