using SharedLib.GlobalInterfaces;

namespace VideohostingPanel.Classes.Interfaces
{
    internal interface IPanelHelper
    {
        public Task<bool> DeleteObjectAsync(IModel model, int id);
        public Task<IModel> UpdateObjectAsync(IModel model);
        public Task<bool> AddObjectAsync(IModel model);
        public Task<IModel> GetObjectAsync(int id);
    }
}
