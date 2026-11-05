using Microsoft.Extensions.DependencyInjection;
using System.IO;
using System.Windows.Media.Imaging;

namespace VideohostingPanel.Services
{
    internal static class PosterLoader
    {
        public static async Task<BitmapImage?> LoadAsync(int videoId)
        {
            try
            {
                var api = App.ServiceProvider.GetRequiredService<ApiService>();

                using var stream = await api.GetPosterAsync(videoId);
                using var memory = new MemoryStream();
                await stream.CopyToAsync(memory);
                memory.Position = 0;

                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = memory;
                bitmap.EndInit();
                bitmap.Freeze();

                return bitmap;
            }
            catch (Exception)
            {
                // Постер не критичен для воспроизведения.
                return null;
            }
        }
    }
}
