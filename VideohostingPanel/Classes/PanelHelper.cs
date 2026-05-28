using SharedLib.GlobalClasses;
using SharedLib.GlobalInterfaces;
using System.IO;
using System.Net.Http;
using VideohostingPanel.Classes.Interfaces;

namespace VideohostingPanel.Classes
{
    public class PanelHelper : IPanelHelper //Создается один на все приложение. Каждое окно в принимаемых параметрах получает ссылку на этот класс.
    {
        public static PanelHelper init;
        private readonly HttpClient _httpClient;

        public PanelHelper()
        {
            init = this;
            _httpClient = new HttpClient();
        }

        public int CountOfRecords { get; set; }

        public async Task<bool> DeleteObjectAsync(IModel model, int id)
        {
            try
            {
                var request = 
            }
            catch (Exception ex)
            {
                throw new Exception("Ошибка при удалении объекта");
            }
        }
        public async Task<IModel> UpdateObject(IModel model);
        public async Task<bool> AddObject(IModel model);
        public async Task<IModel> GetObject(int id);

        public async Task<Stream> GetStreamAsync(int videoId)
        {

        }
    }
}
