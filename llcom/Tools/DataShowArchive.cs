using System.Windows.Media;

namespace llcom.Tools
{
    /// <summary>
    /// DataShow 可序列化 DTO，用于 PrepareDataShowRecord 与 DataShow 构造
    /// </summary>
    public class DataShowRecord
    {
        public string TimeText { get; set; }
        public string TimeTextMs { get; set; }
        public string ArrowText { get; set; }
        public string DataText { get; set; }
        public string DataTextColorHex { get; set; }
        public string RawTitle { get; set; }
        public string RawText { get; set; }
        public string RawTextColorHex { get; set; }
        public string HexPrefix { get; set; }
        public string HexData { get; set; }
        public string HexTextColorHex { get; set; }
        public bool EnableAnsiColor { get; set; }

        public static string BrushToHex(SolidColorBrush b)
        {
            if (b == null) return "#FF32CD32";
            var c = b.Color;
            return $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}";
        }

        public static SolidColorBrush HexToBrush(string hex)
        {
            if (string.IsNullOrEmpty(hex)) return new SolidColorBrush(Color.FromRgb(0x32, 0xCD, 0x32));
            try
            {
                var color = (Color)ColorConverter.ConvertFromString(hex);
                var b = new SolidColorBrush(color);
                b.Freeze();
                return b;
            }
            catch { return new SolidColorBrush(Color.FromRgb(0x32, 0xCD, 0x32)); }
        }
    }
}
