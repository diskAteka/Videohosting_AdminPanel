using System.Text.RegularExpressions;

namespace VideohostingPanel.Services
{
    /// <summary>
    /// БАГ 46: строгая проверка времени в формате ЧЧ:ММ.
    /// TimeSpan.TryParse пропускался: "12" (= 12 суток), "1:2:3" (с секундами) и др.
    /// </summary>
    public static partial class TimeFormatHelper
    {
        [GeneratedRegex(@"^(\d{1,2}):(\d{1,2})$")]
        private static partial Regex HhMmRegex();

        public static bool TryParseHHmm(string? input, out TimeSpan time)
        {
            time = TimeSpan.Zero;
            if (string.IsNullOrWhiteSpace(input))
                return false;

            var match = HhMmRegex().Match(input.Trim());
            if (!match.Success)
                return false;

            if (!int.TryParse(match.Groups[1].Value, out int hours) ||
                !int.TryParse(match.Groups[2].Value, out int minutes))
                return false;

            if (hours < 0 || hours > 23 || minutes < 0 || minutes > 59)
                return false;

            time = new TimeSpan(hours, minutes, 0);
            return true;
        }
    }
}
