using llcom;
using llcom.Tools;
using ScottPlot.Colormaps;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.StartPanel;

namespace llcom.Pages
{
    /// <summary>
    /// DataShowPage.xaml 的交互逻辑
    /// </summary>
    [PropertyChanged.AddINotifyPropertyChangedInterface]
    public partial class DataShowPage : Page
    {
        public DataShowPage()
        {
            InitializeComponent();
        }

        /// <summary>
        /// 禁止自动滚动？
        /// </summary>
        public bool LockLog { get; set; } = false;

        /// <summary>
        /// 停止打印（全局）
        /// </summary>
        public bool DisableLog
        {
            get => Tools.Global.setting?.DisableLog ?? false;
            set { if (Tools.Global.setting != null) Tools.Global.setting.DisableLog = value; }
        }

        /// <summary>
        /// 控制字符（将控制字符显示为可读符号，如 \r \n）
        /// </summary>
        public bool EnableSymbol
        {
            get => Tools.Global.setting?.EnableSymbol ?? true;
            set { if (Tools.Global.setting != null) Tools.Global.setting.EnableSymbol = value; }
        }

        /// <summary>
        /// 时间戳显示格式：0=不显示，1=日期时间，2=UTC时间戳
        /// </summary>
        public int ShowTimestampFormat
        {
            get => Tools.Global.setting?.showTimestampFormat ?? 1;
            set { if (Tools.Global.setting != null) Tools.Global.setting.showTimestampFormat = value; }
        }

        /// <summary>
        /// 显示原始数据（脚本处理前的发送数据）
        /// </summary>
        public bool ShowSendRaw
        {
            get => Tools.Global.setting?.showSendRaw ?? true;
            set { if (Tools.Global.setting != null) Tools.Global.setting.showSendRaw = value; }
        }

        /// <summary>
        /// 显示发送（脚本处理后的发送数据）
        /// </summary>
        public bool ShowSend
        {
            get => Tools.Global.setting?.showSend ?? true;
            set { if (Tools.Global.setting != null) Tools.Global.setting.showSend = value; }
        }

        /// <summary>
        ///  Hex 显示格式（0=同时显示，1=只显示文本，2=只显示 Hex）
        /// </summary>
        public int ShowHexFormat
        {
            get => Tools.Global.setting?.showHexFormat ?? 0;
            set { if (Tools.Global.setting != null) Tools.Global.setting.showHexFormat = value; }
        }

        /// <summary>
        /// ANSI 终端（选中时解析 ANSI 转义序列，不选中时按纯文本显示）
        /// </summary>
        public bool EnableAnsiColor
        {
            get => Tools.Global.setting?.enableAnsiColor ?? true;
            set { if (Tools.Global.setting != null) Tools.Global.setting.enableAnsiColor = value; }
        }

        /// <summary>
        /// 键盘输入（选中数据区可直接使用键盘输入到串口）
        /// </summary>
        public bool Terminal
        {
            get => Tools.Global.setting?.terminal ?? true;
            set { if (Tools.Global.setting != null) Tools.Global.setting.terminal = value; }
        }

        private const int MaxVisibleItems = 200;
        private const int LoadArchiveBatchSize = 50;
        private bool loaded = false;
        private bool _scrollToEndPending = false;
        private bool _isLoadingArchive = false;
        private DispatcherTimer _batchTimer;

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            if (loaded)
                return;
            loaded = true;
            Tools.Logger.DataClearEvent += (xx, x) =>
            {
                MainList.Items.Clear();
                MainTextBox.Document.Blocks.Clear();
                DataShowArchive.Clear();
            };
            MainListScrollViewer.ScrollChanged += MainListScrollViewer_ScrollChanged;
            _batchTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
            _batchTimer.Tick += BatchTimer_Tick;
            _batchTimer.Start();
            LockLogCheckBox.DataContext = this;
            DisableLogCheckBox.DataContext = this;
            ShowSymbolCheckBox.DataContext = this;
            ShowRawDataCheckBox.DataContext = this;
            ShowSendCheckBox.DataContext = this;
            ShowHexFormatCheckBox.DataContext = this;
            ShowTimestampCheckBox.DataContext = this;

            MainList.DataContext = Tools.Global.setting;
            MainTextBox.DataContext = Tools.Global.setting;

            MainListScrollViewer.Visibility = Visibility.Visible;
            MainTextBox.Visibility = Visibility.Collapsed;
        }

        private void BatchTimer_Tick(object sender, EventArgs e)
        {
            if (Tools.Global.isMainWindowsClosed)
                return;
            var batch = new List<Tools.DataShow>();
            if (Tools.Logger.DequeueBatch(batch, 20) == 0)
                return;

            int added = 0;
            foreach (var item in batch)
            {
                var para = item as Tools.DataShowPara;
                var ifKey = para?.interfaceKey ?? "Serial";
                if (!Tools.Global.setting.GetShowSendForInterface(ifKey) && !Tools.Global.setting.GetShowSendRawForInterface(ifKey) && para != null && para.send)
                    continue;

                var isRaw = item is Tools.DataShowRaw;
                var raw = item as Tools.DataShowRaw;
                var showPara = item as Tools.DataShowPara;
                var dataCopy = item.data;
                var timeCopy = item.time;

                DataShow data = isRaw
                    ? new DataShow(raw.title, dataCopy, timeCopy, raw.color)
                    : new DataShow(dataCopy, timeCopy, showPara.send, showPara.interfaceKey, showPara.isRawSend);
                if (data != null)
                {
                    MainList.Items.Add(data);
                    added++;
                }
            }

            if (added > 0 && !LockLog && !_scrollToEndPending)
            {
                _scrollToEndPending = true;
                Dispatcher.BeginInvoke(DispatcherPriority.Background, (Action)(() =>
                {
                    if (!LockLog)
                        MainListScrollViewer.ScrollToEnd();
                    _scrollToEndPending = false;
                }));
            }

            EvictToArchiveIfNeeded();
        }

        private void EvictToArchiveIfNeeded()
        {
            while (MainList.Items.Count > MaxVisibleItems)
            {
                var toEvict = new List<DataShowRecord>();
                int evictCount = Math.Min(MainList.Items.Count - MaxVisibleItems, LoadArchiveBatchSize);
                for (int i = 0; i < evictCount; i++)
                {
                    var item = MainList.Items[0] as DataShow;
                    if (item == null) break;
                    toEvict.Add(DataShowToRecord(item));
                    MainList.Items.RemoveAt(0);
                }
                if (toEvict.Count > 0)
                    DataShowArchive.Append(toEvict);
            }
        }

        private static DataShowRecord DataShowToRecord(DataShow ds)
        {
            return new DataShowRecord
            {
                TimeText = ds.TimeText,
                TimeTextMs = ds.TimeTextMs,
                ArrowText = ds.ArrowText,
                DataText = ds.DataText,
                DataTextColorHex = DataShowRecord.BrushToHex(ds.DataTextColor),
                RawTitle = ds.RawTitle,
                RawText = ds.RawText,
                RawTextColorHex = DataShowRecord.BrushToHex(ds.RawTextColor),
                HexPrefix = ds.HexPrefix,
                HexData = ds.HexData,
                HexTextColorHex = DataShowRecord.BrushToHex(ds.HexTextColor),
                EnableAnsiColor = ds.EnableAnsiColor
            };
        }

        private void MainListScrollViewer_ScrollChanged(object sender, System.Windows.Controls.ScrollChangedEventArgs e)
        {
            if (_isLoadingArchive || !DataShowArchive.HasMoreData()) return;
            if (MainListScrollViewer.VerticalOffset > 30) return;

            _isLoadingArchive = true;
            var records = DataShowArchive.ReadBatch(LoadArchiveBatchSize);
            if (records.Count > 0)
            {
                for (int i = records.Count - 1; i >= 0; i--)
                {
                    var ds = new DataShow(records[i]);
                    if (ds != null)
                        MainList.Items.Insert(0, ds);
                }
                MainListScrollViewer.ScrollToVerticalOffset(0);
            }
            _isLoadingArchive = false;
        }

        private static void AppendAnsiToRichTextBox(System.Windows.Controls.RichTextBox rtb, string text, bool enableAnsi, System.Windows.Media.Brush defaultBrush)
        {
            var inlines = new List<System.Windows.Documents.Inline>();
            var frozenDefault = defaultBrush is SolidColorBrush scb ? CreateFrozenBrush(scb.Color) : CreateFrozenBrush(System.Windows.Media.Colors.Lime);
            if (enableAnsi)
            {
                var segments = AnsiParser.Parse(text);
                foreach (var seg in segments)
                {
                    if (string.IsNullOrEmpty(seg.Text)) continue;
                    var brush = seg.Color.HasValue ? CreateFrozenBrush(seg.Color.Value) : frozenDefault;
                    inlines.Add(new Run(seg.Text) { Foreground = brush });
                }
            }
            else
            {
                if (!string.IsNullOrEmpty(text))
                    inlines.Add(new Run(text) { Foreground = frozenDefault });
            }
            if (inlines.Count == 0) return;
            Paragraph p = null;
            if (rtb.Document.Blocks.Count > 0)
            {
                var lastBlock = rtb.Document.Blocks.LastBlock;
                p = lastBlock as Paragraph;
            }
            if (p == null)
            {
                p = new Paragraph { Margin = new Thickness(0) };
                rtb.Document.Blocks.Add(p);
            }
            foreach (var inline in inlines)
                p.Inlines.Add(inline);
        }

        private static SolidColorBrush CreateFrozenBrush(System.Windows.Media.Color color)
        {
            var b = new SolidColorBrush(color);
            b.Freeze();
            return b;
        }

        /// <summary>
        /// 去除首尾换行，并将连续多个换行合并为单个换行，减少空行
        /// </summary>
        private static string NormalizeDisplayText(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            text = text.TrimEnd('\r', '\n').TrimStart('\r', '\n');
            return Regex.Replace(text, @"[\r\n]+", "\n");
        }


        private void ClearLogButton_Click(object sender, RoutedEventArgs e)
        {
            Tools.Logger.ClearData();
        }

        private void OpenLogFolderButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Diagnostics.Process.Start("explorer.exe", Tools.Global.GetTrueProfilePath() + "logs");
            }
            catch
            {
                Tools.MessageBox.Show($"尝试打开文件夹失败，请自行打开该路径：{Tools.Global.GetTrueProfilePath()}logs");
            }
        }

        private void FontSettingsButton_Click(object sender, RoutedEventArgs e)
        {
            var setting = Tools.Global.setting;
            if (setting == null) return;
            using (var dlg = new FontDialog())
            {
                try
                {
                    dlg.Font = new Font(setting.displayAreaFontFamily ?? "Consolas", (float)setting.displayAreaFontSize);
                }
                catch
                {
                    dlg.Font = new Font("Consolas", 15f);
                }
                dlg.FontMustExist = true;
                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    setting.displayAreaFontFamily = dlg.Font.FontFamily.Name;
                    setting.displayAreaFontSize = dlg.Font.Size;
                }
            }
        }

        /// <summary>
        /// 显示要用到的数据结构
        /// </summary>
        public class DataShow
        {
            public string TimeText { get; set; }
            /// <summary>
            /// UTC 时间戳（毫秒），格式 [1707408000000]
            /// </summary>
            public string TimeTextMs { get; set; }
            public string ArrowText { get; set; }
            public string DataText { get; set; }
            public SolidColorBrush DataTextColor { get; set; }
            public string RawTitle { get; set; }
            /// <summary>
            /// 前面要加换行符
            /// </summary>
            public string RawText { get; set; }
            public SolidColorBrush RawTextColor { get; set; }
            /// <summary>
            /// Hex 前缀 "\r\nHex: "
            /// </summary>
            public string HexPrefix { get; set; }
            /// <summary>
            /// Hex 数据（纯 hex 字符串）
            /// </summary>
            public string HexData { get; set; }
            public SolidColorBrush HexTextColor { get; set; }
            public bool EnableAnsiColor { get; set; }


            public DataShow(byte[] data, DateTime time, bool sent, string interfaceKey = null, bool isRawSend = false)
            {
                if (data == null || data.Count() == 0)
                    return;
                byte[] temp = data.ToArray();
                //转换下接收数据（按顺序执行多个 recv 脚本）
                if (!sent)
                {
                    var uartPara = (Tools.Global.recvPara != null && Tools.Global.recvPara.Length >= 2) ? Tools.Global.recvPara[0] : new byte[0];
                    var uartSendRaw = (Tools.Global.recvPara != null && Tools.Global.recvPara.Length >= 2) ? Tools.Global.recvPara[1] : new byte[0];
                    var scripts = Tools.Global.GetEffectiveRecvScriptListForInterface(interfaceKey ?? "Serial");
                    try
                    {
                        foreach (var scriptName in scripts ?? new System.Collections.Generic.List<string>())
                        {
                            if (string.IsNullOrEmpty(scriptName)) continue;
                            if (!System.IO.File.Exists(Tools.Global.ProfilePath + $"user_script_recv_convert/{scriptName}.lua"))
                                continue;
                            var backup = Tools.Global.recvPara;
                            Tools.Global.recvPara = new byte[][] { uartPara, uartSendRaw };
                            try
                            {
                                temp = LuaEnv.LuaLoader.Run(
                                    $"{scriptName}.lua",
                                    new System.Collections.ArrayList { "uartData", temp, "uartPara", uartPara, "uartSendRaw", uartSendRaw },
                                    "user_script_recv_convert/");
                            }
                            finally
                            {
                                Tools.Global.recvPara = backup;
                            }
                            if (temp == null)
                                return;
                        }
                    }
                    catch (Exception ex)
                    {
                        Tools.MessageBox.Show($"receive convert lua script error\r\n" + ex.ToString());
                        return;
                    }
                    if (temp == null)
                        return;
                }

                TimeText = time.ToString("[yyyy/MM/dd HH:mm:ss.fff]");
                TimeTextMs = $"[{new DateTimeOffset(time.ToUniversalTime()).ToUnixTimeMilliseconds()}]";
                ArrowText = sent ? (isRawSend ? " ↓ " : " ← ") : " → ";
                var ifKey = interfaceKey ?? "Serial";
                DataTextColor = sent ? Tools.Global.setting.GetSendDisplayBrushForInterface(ifKey) : Tools.Global.setting.GetRecvDisplayBrushForInterface(ifKey);
                HexTextColor = DataTextColor;
                EnableAnsiColor = Tools.Global.setting.enableAnsiColor;

                var len = temp.Length;
                var fmt = Tools.Global.setting.GetShowHexFormatForInterface(interfaceKey ?? "Serial");
                var enableSym = Tools.Global.setting.GetEnableSymbolForInterface(interfaceKey ?? "Serial");
                //主要数据
                if (temp != null && temp.Length > 0)
                {
                    var raw = (fmt switch
                    {
                        2 => Tools.Global.Byte2Hex(temp, " ", len),
                        _ => Tools.Global.Byte2Readable(temp, len, enableSym),
                    }) ?? "";
                    DataText = EnableAnsiColor ? NormalizeDisplayText(raw) : raw.TrimEnd('\r', '\n');
                    //同时显示模式时，才显示小字hex
                    if (fmt == 0)
                    {
                        HexPrefix = "\r\nHex: ";
                        HexData = Tools.Global.Byte2Hex(temp, " ", len);
                    }
                }
            }

            public DataShow(string title, byte[] data, DateTime time, SolidColorBrush color)
            {
                byte[] temp = data.ToArray();

                TimeText = time.ToString("[yyyy/MM/dd HH:mm:ss.fff]");
                TimeTextMs = $"[{new DateTimeOffset(time.ToUniversalTime()).ToUnixTimeMilliseconds()}]";

                var len = temp.Length;
                // DataShowRaw 无 interfaceKey，使用当前选中接口或全局
                var mw = System.Windows.Application.Current.MainWindow as MainWindow;
                var ifKey = (mw?.DataInterfaceComboBox?.SelectedIndex ?? 0) switch
                {
                    0 => "Serial", 1 => "UdpClient", 2 => "TcpClient", 3 => "TcpSslClient", 4 => "UdpLocal", 5 => "TcpLocal",
                    6 => "Tcp", 7 => "WinUSB", 8 => "SerialMonitor", 9 => "MQTT", _ => "Serial"
                };
                var fmt = Tools.Global.setting.GetShowHexFormatForInterface(ifKey);
                var enableSym = Tools.Global.setting.GetEnableSymbolForInterface(ifKey);
                //主要数据
                if (temp != null && temp.Length > 0)
                {
                    var raw = (fmt switch
                    {
                        2 => Tools.Global.Byte2Hex(temp, " ", len),
                        _ => Tools.Global.Byte2Readable(temp, len, enableSym),
                    }) ?? "";
                    var enableAnsi = Tools.Global.setting.enableAnsiColor;
                    RawText = "\n" + (enableAnsi ? NormalizeDisplayText(raw) : raw.TrimEnd('\r', '\n'));
                    //同时显示模式时，才显示小字hex
                    if (fmt == 0)
                    {
                        HexPrefix = "\r\nHex: ";
                        HexData = Tools.Global.Byte2Hex(temp, " ", len);
                    }
                }

                RawTitle = title;
                RawTextColor = color;
                HexTextColor = color;
                EnableAnsiColor = Tools.Global.setting.enableAnsiColor;
            }

            public DataShow(DataShowRecord r)
            {
                if (r == null) return;
                TimeText = r.TimeText;
                TimeTextMs = r.TimeTextMs;
                ArrowText = r.ArrowText;
                DataText = r.DataText;
                DataTextColor = DataShowRecord.HexToBrush(r.DataTextColorHex);
                RawTitle = r.RawTitle;
                RawText = r.RawText;
                RawTextColor = DataShowRecord.HexToBrush(r.RawTextColorHex);
                HexPrefix = r.HexPrefix;
                HexData = r.HexData;
                HexTextColor = DataShowRecord.HexToBrush(r.HexTextColorHex);
                EnableAnsiColor = r.EnableAnsiColor;
            }
        }

        private void SaveLogButton_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.Filter = "Log files(*.log)|*.log";
            saveFileDialog.InitialDirectory = Tools.Global.GetTrueProfilePath() + "logs";
            saveFileDialog.FileName = DateTime.Now.ToString("yyMMddHHmmss") + ".log";
            if(saveFileDialog.ShowDialog() == DialogResult.OK)
            {
                string saveFilePath = saveFileDialog.FileName;
                FileStream fs = new FileStream(saveFilePath, FileMode.Create);
                StreamWriter sw = new StreamWriter(fs, Encoding.UTF8);
                {
                    int iCount = MainList.Items.Count - 1;
                    for (int i = 0; i <= iCount; i++)
                    {
                        var item = MainList.Items[i] as DataShow;
                        if (string.IsNullOrEmpty(item.RawTitle))
                            sw.WriteLine(item.TimeText + (item.ArrowText == " ← " ? " [send] " : " [recv] ") + item.DataText);
                        else
                            sw.WriteLine(item.TimeText + " [" + item.RawTitle + "] " + item.RawText);
                    }
                }
                sw.Flush();
                sw.Close();
                fs.Close();
            }
        }
    }
}
