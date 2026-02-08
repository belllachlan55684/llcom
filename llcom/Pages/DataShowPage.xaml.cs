using llcom;
using llcom.Tools;
using ScottPlot.Drawing.Colormaps;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
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
        /// 显示时间戳？
        /// </summary>
        public bool ShowTimestamp { get; set; } = true;
        private bool loaded = false;
        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            if (loaded)
                return;
            loaded = true;
            //添加待显示数据到缓冲区
            Tools.Logger.DataShowTask += Logger_DataShowTask;
            Tools.Logger.DataClearEvent += (xx,x) =>
            {
                MainList.Items.Clear();
                MainTextBox.Clear();
            };
            LockLogCheckBox.DataContext = this;
            ShowTimestampCheckBox.DataContext = this;

            MainList.DataContext = Tools.Global.setting;
            MainTextBox.DataContext = Tools.Global.setting;

            lastPackShowMode = Tools.Global.setting.timeout >= 0;
            MainListScrollViewer.Visibility = lastPackShowMode ? Visibility.Visible : Visibility.Collapsed;
            MainTextBox.Visibility = lastPackShowMode ? Visibility.Collapsed : Visibility.Visible;
        }

        //记录一下上次是不是分包显示的
        bool lastPackShowMode = false;
        private void Logger_DataShowTask(object sender, Tools.DataShow e)
        {
            //先判断下要不要清空
            var needPack = Tools.Global.setting.timeout >= 0;
            if (lastPackShowMode != needPack)
            {
                lastPackShowMode = needPack;
                DoInvoke(() =>
                {
                    MainList.Items.Clear();
                    MainTextBox.Clear();
                    MainListScrollViewer.Visibility = needPack ? Visibility.Visible : Visibility.Collapsed;
                    MainTextBox.Visibility = needPack ? Visibility.Collapsed : Visibility.Visible;
                });
            }

            //如果不开回显，就别打印
            var para = e as DataShowPara;
            var ifKey = para?.interfaceKey ?? "Serial";
            if (!Tools.Global.setting.GetShowSendForInterface(ifKey) && !Tools.Global.setting.GetShowSendRawForInterface(ifKey) && para != null && para.send)
                return;

            //显示到列表
            if (!needPack && e is not DataShowRaw)//不分包模式
            {
                var fmt = Tools.Global.setting.GetShowHexFormatForInterface(ifKey);
                var enableSym = Tools.Global.setting.GetEnableSymbolForInterface(ifKey);
                var DataText = fmt switch
                {
                    2 => Tools.Global.Byte2Hex(e.data, " ", e.data.Length) + " ",
                    _ => Tools.Global.Byte2Readable(e.data, e.data.Length, enableSym),
                };
                DoInvoke(() =>
                {
                    MainTextBox.AppendText(DataText);
                    if (!LockLog)
                        MainTextBox.ScrollToEnd();
                });
            }
            else//分包模式：必须在 UI 线程创建 DataShow，否则 GetSendDisplayBrush/GetRecvDisplayBrush 创建的 SolidColorBrush 会导致跨线程 DependencySource 异常
            {
                var isRaw = e is DataShowRaw;
                var raw = e as DataShowRaw;
                var showPara = e as DataShowPara;
                var dataCopy = e.data;
                var timeCopy = e.time;
                DoInvoke(() =>
                {
                    var data = isRaw
                        ? new DataShow(raw.title, dataCopy, timeCopy, raw.color)
                        : new DataShow(dataCopy, timeCopy, showPara.send, showPara.interfaceKey);
                    if (data != null)
                    {
                        MainList.Items.Add(data);
                        if (!LockLog)
                            MainListScrollViewer.ScrollToEnd();
                    }
                });
            }
        }

        private bool DoInvoke(Action action)
        {
            if (Tools.Global.isMainWindowsClosed)
                return false;
            Dispatcher.Invoke(action);
            return true;
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

        /// <summary>
        /// 显示要用到的数据结构
        /// </summary>
        public class DataShow
        {
            public string TimeText { get; set; }
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
            /// 前面要加换行符
            /// </summary>
            public string HexText { get; set; }
            public SolidColorBrush HexTextColor { get; set; }


            public DataShow(byte[] data, DateTime time, bool sent, string interfaceKey = null)
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
                ArrowText = sent ? " ← " : " → ";
                DataTextColor = sent ? Tools.Global.setting.GetSendDisplayBrush() : Tools.Global.setting.GetRecvDisplayBrush();
                HexTextColor = DataTextColor;

                var len = temp.Length;
                var fmt = Tools.Global.setting.GetShowHexFormatForInterface(interfaceKey ?? "Serial");
                var enableSym = Tools.Global.setting.GetEnableSymbolForInterface(interfaceKey ?? "Serial");
                //主要数据
                if (temp != null && temp.Length > 0)
                {
                    DataText = fmt switch
                    {
                        2 => Tools.Global.Byte2Hex(temp, " ", len),
                        _ => Tools.Global.Byte2Readable(temp, len, enableSym),
                    };
                    //同时显示模式时，才显示小字hex
                    if (fmt == 0)
                        HexText = "\nHex: " + Tools.Global.Byte2Hex(temp, " ", len);
                }
            }

            public DataShow(string title, byte[] data, DateTime time, SolidColorBrush color)
            {
                byte[] temp = data.ToArray();

                TimeText = time.ToString("[yyyy/MM/dd HH:mm:ss.fff]");

                var len = temp.Length;
                // DataShowRaw 无 interfaceKey，使用当前选中接口或全局
                var mw = System.Windows.Application.Current.MainWindow as MainWindow;
                var ifKey = (mw?.DataInterfaceComboBox?.SelectedIndex ?? 0) switch
                {
                    0 => "Serial", 1 => "TcpClient", 2 => "UdpLocal", 3 => "TcpLocal", 4 => "Tcp", 5 => "WinUSB",
                    6 => "SerialMonitor", 7 => "MQTT", _ => "Serial"
                };
                var fmt = Tools.Global.setting.GetShowHexFormatForInterface(ifKey);
                var enableSym = Tools.Global.setting.GetEnableSymbolForInterface(ifKey);
                //主要数据
                if (temp != null && temp.Length > 0)
                {
                    RawText = "\n" + fmt switch
                    {
                        2 => Tools.Global.Byte2Hex(temp, " ", len),
                        _ => Tools.Global.Byte2Readable(temp, len, enableSym),
                    };
                    //同时显示模式时，才显示小字hex
                    if (fmt == 0)
                        HexText = "\nHex: " + Tools.Global.Byte2Hex(temp, " ", len);
                }

                RawTitle = title;
                RawTextColor = color;
                HexTextColor = color;
            }
        }

        private void SaveLogButton_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.Filter = "Log files(*.log)|*.log";
            saveFileDialog.InitialDirectory = Tools.Global.GetTrueProfilePath() + "logs";
            if(saveFileDialog.ShowDialog() == DialogResult.OK)
            {
                string saveFilePath = saveFileDialog.FileName;
                var needPack = Tools.Global.setting.timeout >= 0;
                FileStream fs = new FileStream(saveFilePath, FileMode.Create);
                StreamWriter sw = new StreamWriter(fs, Encoding.UTF8);
                if (!needPack)
                {
                    sw.Write(MainTextBox.Text);
                }
                else
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
