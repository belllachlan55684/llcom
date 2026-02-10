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
        /// 显示发送（脚本处理后的发送数据）
        /// </summary>
        public bool ShowSend
        {
            get => Tools.Global.setting?.showSend ?? true;
            set { if (Tools.Global.setting != null) Tools.Global.setting.showSend = value; }
        }

        /// <summary>
        ///  Hex 显示格式（1=只显示文本，2=只显示 Hex）
        /// </summary>
        public int ShowHexFormat
        {
            get => Tools.Global.setting?.showHexFormat ?? 1;
            set { if (Tools.Global.setting != null) Tools.Global.setting.showHexFormat = value; }
        }

        /// <summary>
        /// ANSI 终端（选中时解析 ANSI 转义序列，不选中时按纯文本显示）
        /// </summary>
        /// <summary>
        /// 是否解析内容中的 ANSI 转义颜色（不改变其他选项或显示控件）
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

        private static readonly object _luaRunLock = new object();
        private bool loaded = false;
        private bool _scrollToEndPending = false;
        private int _clearGeneration = 0;       // 清空时递增，飞行中的批次若发现已清空则跳过添加
        private ScrollViewer _mainTextBoxScrollViewer; // MainTextBox 内部 ScrollViewer，用于 ScrollToEnd
        private static long _maxShowDataProcessMs; // 单条完整处理最大耗时（ms，取条到 UI 更新结束）
        private static long _maxDataProcessMs;     // 数据处理阶段最大耗时（ms）
        private static long _maxUiUpdateMs;       // UI 更新阶段最大耗时（ms）

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            if (loaded)
                return;
            loaded = true;
            Tools.Logger.DataClearEvent += (xx, x) =>
            {
                _clearGeneration++;
                MainTextBox.Document.Blocks.Clear();
            };
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, (Action)(() =>
            {
                _mainTextBoxScrollViewer = FindScrollViewer(MainTextBox);
            }));
            _ = Task.Run(ShowDataConsumerLoop);
            LockLogCheckBox.DataContext = this;
            DisableLogCheckBox.DataContext = this;
            ShowSymbolCheckBox.DataContext = this;
            ShowSendCheckBox.DataContext = this;
            ShowHexFormatCheckBox.DataContext = this;
            ShowTimestampCheckBox.DataContext = this;

            MainTextBox.DataContext = Tools.Global.setting;
        }

        private string GetRawInterfaceKeyFromUi()
        {
            var mw = System.Windows.Application.Current?.MainWindow as MainWindow;
            var idx = mw?.DataInterfaceComboBox?.SelectedIndex ?? 0;
            return idx switch
            {
                0 => "Serial", 1 => "UdpClient", 2 => "TcpClient", 3 => "TcpSslClient", 4 => "UdpLocal", 5 => "TcpLocal",
                6 => "Tcp", 7 => "WinUSB", 8 => "SerialMonitor", 9 => "MQTT", _ => "Serial"
            };
        }

        private void ShowDataConsumerLoop()
        {
            while (!Tools.Global.isMainWindowsClosed)
            {
                if (!Tools.Logger.TryTakeOne(out var item, 50))
                    continue;
                if (Tools.Global.isMainWindowsClosed)
                    break;

                var swTotal = Stopwatch.StartNew();
                var snapshot = Dispatcher.Invoke(() => new
                {
                    enableAnsi = Tools.Global.setting?.enableAnsiColor ?? true,
                    rawInterfaceKey = GetRawInterfaceKeyFromUi(),
                    gen = _clearGeneration
                });

                var para = item as Tools.DataShowPara;
                var ifKey = para?.interfaceKey ?? "Serial";
                if (para != null && para.send && !Tools.Global.setting.GetShowSendForInterface(ifKey))
                {
                    swTotal.Stop();
                    continue;
                }

                var dataCopy = item.data?.ToArray();
                if (dataCopy == null)
                {
                    swTotal.Stop();
                    continue;
                }

                var swData = Stopwatch.StartNew();
                DataShowPrepareInput pin;
                var isRaw = item is Tools.DataShowRaw;
                var raw = item as Tools.DataShowRaw;
                var showPara = item as Tools.DataShowPara;
                if (isRaw)
                {
                    pin = new DataShowPrepareInput
                    {
                        Data = dataCopy, Time = item.time, IsRaw = true,
                        Title = raw.title, ColorHex = DataShowRecord.BrushToHex(raw.color), InterfaceKey = snapshot.rawInterfaceKey
                    };
                }
                else
                {
                    pin = new DataShowPrepareInput
                    {
                        Data = dataCopy, Time = item.time, IsRaw = false,
                        Send = showPara.send, InterfaceKey = showPara.interfaceKey ?? "Serial", IsRawSend = showPara.isRawSend
                    };
                }

                var record = PrepareDataShowRecord(pin, snapshot.enableAnsi);
                if (record == null)
                {
                    swTotal.Stop();
                    continue;
                }

                swData.Stop();
                var elapsedDataMs = swData.ElapsedMilliseconds;

                var gen = snapshot.gen;
                var swUi = Stopwatch.StartNew();
                Dispatcher.Invoke(() =>
                {
                    if (gen != _clearGeneration)
                        return;
                    var parseAnsi = Tools.Global.setting?.enableAnsiColor ?? true;
                    AppendRecordToRichTextBox(MainTextBox, record, parseAnsi);
                    if (!LockLog && !_scrollToEndPending && _mainTextBoxScrollViewer != null)
                    {
                        _scrollToEndPending = true;
                        Dispatcher.BeginInvoke(DispatcherPriority.Background, (Action)(() =>
                        {
                            if (!LockLog && _mainTextBoxScrollViewer != null)
                                _mainTextBoxScrollViewer.ScrollToEnd();
                            _scrollToEndPending = false;
                        }));
                    }
                });
                swUi.Stop();
                var elapsedUiMs = swUi.ElapsedMilliseconds;
                swTotal.Stop();
                var elapsedTotalMs = swTotal.ElapsedMilliseconds;

                var updated = false;
                if (elapsedTotalMs > _maxShowDataProcessMs)
                {
                    _maxShowDataProcessMs = elapsedTotalMs;
                    updated = true;
                }
                if (elapsedDataMs > _maxDataProcessMs)
                {
                    _maxDataProcessMs = elapsedDataMs;
                    updated = true;
                }
                if (elapsedUiMs > _maxUiUpdateMs)
                {
                    _maxUiUpdateMs = elapsedUiMs;
                    updated = true;
                }
                if (updated)
                {
                    var depth = Tools.Logger.PendingShowQueueCount;
                    Tools.SystemLog.WriteLine($"[数据显示] 单条完整最大: {_maxShowDataProcessMs} ms, 数据处理最大: {_maxDataProcessMs} ms, UI更新最大: {_maxUiUpdateMs} ms, 队列深度: {depth}");
                }
            }
        }

        private sealed class DataShowPrepareInput
        {
            public byte[] Data;
            public DateTime Time;
            public bool IsRaw;
            public bool Send;
            public string InterfaceKey;
            public bool IsRawSend;
            public string Title;
            public string ColorHex;
        }

        private static DataShowRecord PrepareDataShowRecord(DataShowPrepareInput pin, bool forAnsiMode = false)
        {
            if (pin?.Data == null || pin.Data.Length == 0) return null;
            byte[] temp = pin.Data;
            if (!pin.IsRaw && !pin.Send)
            {
                var uartPara = (Tools.Global.recvPara != null && Tools.Global.recvPara.Length >= 2) ? Tools.Global.recvPara[0] : new byte[0];
                var uartSendRaw = (Tools.Global.recvPara != null && Tools.Global.recvPara.Length >= 2) ? Tools.Global.recvPara[1] : new byte[0];
                var scripts = Tools.Global.GetEffectiveRecvScriptListForInterface(pin.InterfaceKey ?? "Serial");
                lock (_luaRunLock)
                {
                    try
                    {
                        foreach (var scriptName in scripts ?? new List<string>())
                        {
                            if (string.IsNullOrEmpty(scriptName)) continue;
                            if (!File.Exists(Tools.Global.ProfilePath + $"user_script_recv_convert/{scriptName}.lua"))
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
                            finally { Tools.Global.recvPara = backup; }
                            if (temp == null) return null;
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                            Tools.MessageBox.Show($"receive convert lua script error\r\n" + ex.ToString()));
                        return null;
                    }
                }
            }

            var timeText = pin.Time.ToString("[yyyy/MM/dd HH:mm:ss.fff]");
            var timeTextMs = $"[{new DateTimeOffset(pin.Time.ToUniversalTime()).ToUnixTimeMilliseconds()}]";
            var enableAnsi = Tools.Global.setting.enableAnsiColor;
            var len = temp.Length;
            var fmt = Tools.Global.setting.GetShowHexFormatForInterface(pin.InterfaceKey ?? "Serial");
            var enableSym = Tools.Global.setting.GetEnableSymbolForInterface(pin.InterfaceKey ?? "Serial");

            if (pin.IsRaw)
            {
                var raw = (fmt switch
                {
                    2 => Tools.Global.Byte2Hex(temp, " ", len),
                    _ => Tools.Global.Byte2Readable(temp, len, enableSym),
                }) ?? "";
                return new DataShowRecord
                {
                    TimeText = timeText, TimeTextMs = timeTextMs, ArrowText = " → ",
                    RawTitle = pin.Title, RawText = "\n" + (forAnsiMode ? NormalizeDisplayText(raw) : raw.TrimEnd('\r', '\n')),
                    RawTextColorHex = pin.ColorHex, HexTextColorHex = pin.ColorHex, EnableAnsiColor = enableAnsi,
                    HexPrefix = null, HexData = null
                };
            }

            var arrowText = pin.Send ? (pin.IsRawSend ? " ↓ " : " ← ") : " → ";
            var dataTextColorHex = pin.Send ? Tools.Global.setting.GetSendDisplayColorForInterface(pin.InterfaceKey) : Tools.Global.setting.GetRecvDisplayColorForInterface(pin.InterfaceKey);
            if (string.IsNullOrEmpty(dataTextColorHex)) dataTextColorHex = pin.Send ? "#CD5C5C" : "#32CD32";
            var dataRaw = (fmt switch { 2 => Tools.Global.Byte2Hex(temp, " ", len), _ => Tools.Global.Byte2Readable(temp, len, enableSym) }) ?? "";
            return new DataShowRecord
            {
                TimeText = timeText, TimeTextMs = timeTextMs, ArrowText = arrowText,
                DataText = forAnsiMode ? NormalizeDisplayText(dataRaw) : dataRaw.TrimEnd('\r', '\n'),
                DataTextColorHex = dataTextColorHex, HexTextColorHex = dataTextColorHex, EnableAnsiColor = enableAnsi,
                HexPrefix = null, HexData = null
            };
        }

        private static ScrollViewer FindScrollViewer(DependencyObject parent)
        {
            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                if (child is ScrollViewer sv) return sv;
                var found = FindScrollViewer(child);
                if (found != null) return found;
            }
            return null;
        }

        private void DataTextBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is System.Windows.Controls.TextBox tb) tb.SelectAll();
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

        /// <summary>
        /// 将单条 DataShowRecord 按时间戳、主内容、Hex 的颜色逻辑追加到 RichTextBox；parseAnsi 为 true 时对主内容解析 ANSI 转义。
        /// </summary>
        private static void AppendRecordToRichTextBox(System.Windows.Controls.RichTextBox rtb, DataShowRecord record, bool parseAnsi)
        {
            if (record == null) return;
            var inlines = new List<System.Windows.Documents.Inline>();
            var showTs = Tools.Global.setting?.showTimestampFormat ?? 1;
            if (showTs != 0)
            {
                var prefix = showTs == 2 ? (record.TimeTextMs ?? "") + (record.ArrowText ?? "") : (record.TimeText ?? "") + (record.ArrowText ?? "");
                if (!string.IsNullOrEmpty(prefix))
                {
                    var tsBrush = CreateFrozenBrush(System.Windows.Media.Colors.White);
                    inlines.Add(new Run(prefix) { Foreground = tsBrush });
                }
            }
            string mainText;
            string mainColorHex;
            if (!string.IsNullOrEmpty(record.RawTitle) || !string.IsNullOrEmpty(record.RawText))
            {
                mainText = (record.RawTitle ?? "") + (record.RawText ?? "");
                mainColorHex = record.RawTextColorHex;
            }
            else
            {
                mainText = record.DataText ?? "";
                mainColorHex = record.DataTextColorHex;
            }
            if (!string.IsNullOrEmpty(mainText))
            {
                var defaultBrush = DataShowRecord.HexToBrush(mainColorHex);
                var frozenDefault = defaultBrush is SolidColorBrush scb ? CreateFrozenBrush(scb.Color) : CreateFrozenBrush(System.Windows.Media.Colors.Lime);
                if (parseAnsi)
                {
                    var segments = AnsiParser.Parse(mainText);
                    foreach (var seg in segments)
                    {
                        if (string.IsNullOrEmpty(seg.Text)) continue;
                        var brush = seg.Color.HasValue ? CreateFrozenBrush(seg.Color.Value) : frozenDefault;
                        inlines.Add(new Run(seg.Text) { Foreground = brush });
                    }
                }
                else
                {
                    inlines.Add(new Run(mainText) { Foreground = frozenDefault });
                }
            }
            if (!string.IsNullOrEmpty(record.HexPrefix) || !string.IsNullOrEmpty(record.HexData))
            {
                inlines.Add(new Run("\n") { Foreground = CreateFrozenBrush(System.Windows.Media.Colors.Lime) });
                var hexText = (record.HexPrefix ?? "") + (record.HexData ?? "");
                var hexBrush = DataShowRecord.HexToBrush(record.HexTextColorHex);
                var frozenHex = hexBrush is SolidColorBrush hb ? CreateFrozenBrush(hb.Color) : CreateFrozenBrush(System.Windows.Media.Colors.Lime);
                inlines.Add(new Run(hexText) { Foreground = frozenHex });
            }
            inlines.Add(new Run("\n") { Foreground = CreateFrozenBrush(System.Windows.Media.Colors.Lime) });
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

        private void SaveLogButton_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.Filter = "Log files(*.log)|*.log";
            saveFileDialog.InitialDirectory = Tools.Global.GetTrueProfilePath() + "logs";
            saveFileDialog.FileName = DateTime.Now.ToString("yyMMddHHmmss") + ".log";
            if (saveFileDialog.ShowDialog() == DialogResult.OK)
            {
                string saveFilePath = saveFileDialog.FileName;
                try
                {
                    var range = new System.Windows.Documents.TextRange(MainTextBox.Document.ContentStart, MainTextBox.Document.ContentEnd);
                    var text = range.Text ?? "";
                    File.WriteAllText(saveFilePath, text, Encoding.UTF8);
                }
                catch (Exception ex)
                {
                    Tools.MessageBox.Show($"保存失败：{ex.Message}");
                }
            }
        }
    }
}
