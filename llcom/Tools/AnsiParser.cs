using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Windows.Media;

namespace llcom.Tools
{
    /// <summary>
    /// 解析 ANSI SGR 转义序列，输出带颜色的文本段
    /// </summary>
    public static class AnsiParser
    {
        private const char EscapeChar = '\x1b';

        /// <summary>
        /// 解析包含 ANSI 转义序列的字符串，返回 (文本, 颜色?) 列表。Color 为 null 时使用默认前景色。
        /// </summary>
        public static List<AnsiSegment> Parse(string text)
        {
            if (string.IsNullOrEmpty(text))
                return new List<AnsiSegment>();

            var result = new List<AnsiSegment>();
            int startIndex = 0;
            Color? foreground = null;
            bool isBright = false;

            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == EscapeChar && text.Length >= i + 4 && text[i + 1] == '[')
                {
                    // 找到下一个 'm' 或字符串结束
                    int mIdx = text.IndexOf('m', i + 2);
                    if (mIdx < 0)
                        break;

                    if (startIndex < i)
                    {
                        result.Add(new AnsiSegment(text.Substring(startIndex, i - startIndex), foreground));
                    }

                    string paramStr = text.Substring(i + 2, mIdx - i - 2);
                    var codes = ParseSgrParams(paramStr);

                    foreach (var code in codes)
                    {
                        if (code == 0)
                        {
                            foreground = null;
                            isBright = false;
                        }
                        else if (code == 1)
                        {
                            isBright = true;
                        }
                        else if (code >= 30 && code <= 37)
                        {
                            foreground = GetForegroundColor(code, isBright);
                            isBright = false;
                        }
                        else if (code == 39)
                        {
                            foreground = null;
                        }
                    }

                    startIndex = mIdx + 1;
                    i = mIdx;
                }
            }

            if (startIndex < text.Length)
            {
                result.Add(new AnsiSegment(text.Substring(startIndex), foreground));
            }

            return result;
        }

        /// <summary>
        /// 去除 ANSI 转义序列，返回纯文本
        /// </summary>
        public static string Strip(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;
            return Regex.Replace(text, @"\x1b\[[0-9;]*m", "");
        }

        private static List<int> ParseSgrParams(string paramStr)
        {
            var codes = new List<int>();
            if (string.IsNullOrEmpty(paramStr))
            {
                codes.Add(0);
                return codes;
            }
            foreach (var part in paramStr.Split(';'))
            {
                if (int.TryParse(part.Trim(), out int code))
                    codes.Add(code);
            }
            if (codes.Count == 0)
                codes.Add(0);
            return codes;
        }

        private static Color GetForegroundColor(int code, bool isBright)
        {
            // 30-37: 前景色
            switch (code)
            {
                case 30: return Colors.Black;
                case 31: return isBright ? ColorFromRgb(255, 85, 85) : ColorFromRgb(187, 0, 0);   // Red
                case 32: return isBright ? ColorFromRgb(85, 255, 85) : ColorFromRgb(0, 187, 0);   // Green
                case 33: return isBright ? ColorFromRgb(255, 255, 85) : ColorFromRgb(187, 187, 0); // Yellow
                case 34: return isBright ? ColorFromRgb(85, 85, 255) : ColorFromRgb(0, 0, 187);   // Blue
                case 35: return isBright ? ColorFromRgb(255, 85, 255) : ColorFromRgb(187, 0, 187); // Magenta
                case 36: return isBright ? ColorFromRgb(85, 255, 255) : ColorFromRgb(0, 187, 187);  // Cyan
                case 37: return isBright ? Colors.White : ColorFromRgb(187, 187, 187);             // White
                default: return Colors.White;
            }
        }

        private static Color ColorFromRgb(byte r, byte g, byte b) =>
            Color.FromRgb(r, g, b);
    }

    public struct AnsiSegment
    {
        public string Text { get; }
        public Color? Color { get; }

        public AnsiSegment(string text, Color? color)
        {
            Text = text ?? "";
            Color = color;
        }
    }
}
