namespace VideohostingPanel.Services
{
    public class ComboBoxItem
    {
        public int Id { get; set; }
        public string DisplayName { get; set; } = string.Empty;

        // Ключ таблицы на сервере (TableToClassMapper.TableToClassMap)
        public string ServerKey { get; set; } = string.Empty;

        public override string ToString() => DisplayName;
    }
}
