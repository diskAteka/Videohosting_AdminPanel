namespace VideohostingPanel.Services
{
    /// <summary>Допустимые значения ServerLog.ActionType (CHECK-констрейнт в БД).</summary>
    public static class AuditAction
    {
        public const string Insert = "INSERT";
        public const string Update = "UPDATE";
        public const string Delete = "DELETE";
    }

    /// <summary>Имена таблиц для ServerLog.AffectedTable.</summary>
    public static class AuditTable
    {
        public const string Video = "Video";
        public const string User = "User";
        public const string Comment = "Comment";
        public const string Employee = "Employee";
    }
}
