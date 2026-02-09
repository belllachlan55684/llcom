using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Media;

namespace llcom.Tools
{
    /// <summary>
    /// DataShow 可序列化 DTO，用于磁盘归档
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

    /// <summary>
    /// 数据量上限双缓存：内存 + 磁盘归档，支持向上滚动懒加载
    /// </summary>
    public static class DataShowArchive
    {
        private static string ArchivePath => Global.ProfilePath + $"temp/data_show_archive_{Global.InstanceId}.tmp";
        private static int _archiveReadLineIndex = 0;
        private static readonly object _archiveLock = new object();

        public static void Append(IEnumerable<DataShowRecord> records)
        {
            if (records == null) return;
            var dir = Path.GetDirectoryName(ArchivePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            lock (_archiveLock)
            {
                using (var sw = new StreamWriter(ArchivePath, true, System.Text.Encoding.UTF8))
                {
                    foreach (var r in records)
                    {
                        sw.WriteLine(JsonConvert.SerializeObject(r));
                    }
                }
            }
        }

        /// <summary>
        /// 从归档读取一批（最多 maxCount 条），返回反序列化后的记录；已读过的行不会重复返回
        /// </summary>
        public static List<DataShowRecord> ReadBatch(int maxCount = 50)
        {
            var result = new List<DataShowRecord>();
            if (!File.Exists(ArchivePath)) return result;

            lock (_archiveLock)
            {
                try
                {
                    var lines = File.ReadAllLines(ArchivePath);
                    int start = _archiveReadLineIndex;
                    int end = Math.Min(start + maxCount, lines.Length);
                    for (int i = start; i < end; i++)
                    {
                        if (string.IsNullOrWhiteSpace(lines[i])) continue;
                        try
                        {
                            var r = JsonConvert.DeserializeObject<DataShowRecord>(lines[i]);
                            if (r != null) result.Add(r);
                        }
                        catch { }
                    }
                    _archiveReadLineIndex = end;
                }
                catch { }
            }
            return result;
        }

        /// <summary>
        /// 归档中是否还有未读数据
        /// </summary>
        public static bool HasMoreData()
        {
            if (!File.Exists(ArchivePath)) return false;
            lock (_archiveLock)
            {
                var lines = File.ReadAllLines(ArchivePath);
                return _archiveReadLineIndex < lines.Length;
            }
        }

        public static void Clear()
        {
            lock (_archiveLock)
            {
                _archiveReadLineIndex = 0;
                try
                {
                    if (File.Exists(ArchivePath))
                        File.Delete(ArchivePath);
                }
                catch { }
            }
        }
    }
}
