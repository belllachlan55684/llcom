using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Management;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using llcom.Tools;

namespace llcom.Pages
{
    /// <summary>
    /// SerialPortPage.xaml 的交互逻辑
    /// </summary>
    public partial class SerialPortPage : Page
    {
        private bool forcusClosePort = true;
        private bool isOpeningPort = false;
        private byte[] toSendData = null;
        private int lastBaudRateSelectedIndex = -1;
        private bool refreshLock = false;
        private bool skipSearch = false;
        private int searchCount = 0;

        public SerialPortPage()
        {
            InitializeComponent();
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            MainGrid.DataContext = Tools.Global.setting;
            toSendDataTextBox.DataContext = Tools.Global.setting;

            var br = Tools.Global.setting.baudRate.ToString();
            if (baudRateComboBox.Items.Contains(br))
                baudRateComboBox.Text = Tools.Global.setting.baudRate.ToString();
            else
            {
                lastBaudRateSelectedIndex = baudRateComboBox.Items.Count - 1;
                baudRateComboBox.Items[baudRateComboBox.Items.Count - 1] = br;
                baudRateComboBox.Text = br;
            }

            RefreshPortList();
        }

        /// <summary>
        /// 供 MainWindow 调用的刷新串口列表
        /// </summary>
        public void RefreshPortList(string lastPort = null)
        {
            if (refreshLock)
                return;
            refreshLock = true;
            serialPortsListComboBox.Items.Clear();
            List<string> strs = new List<string>();
            searchCount = 0;
            Task.Run(() =>
            {
                while (!skipSearch)
                {
                    try
                    {
                        ManagementObjectSearcher searcher = new ManagementObjectSearcher("root\\CIMV2", "SELECT * FROM Win32_PnPEntity");
                        Regex regExp = new Regex("\\(COM\\d+\\)");
                        foreach (ManagementObject queryObj in searcher.Get())
                        {
                            if ((queryObj["Caption"] != null) && regExp.IsMatch(queryObj["Caption"].ToString()))
                                strs.Add(queryObj["Caption"].ToString());
                        }
                        break;
                    }
                    catch (Exception ex)
                    {
                        if (++searchCount >= 3)
                        {
                            skipSearch = true;
                            Tools.MessageBox.Show(ex.Message);
                        }
                        else
                            Task.Delay(500).Wait();
                    }
                }

                try
                {
                    foreach (string p in SerialPort.GetPortNames())
                    {
                        var pp = p;
                        if (p.IndexOf("\0") > 0)
                            pp = p.Substring(0, p.IndexOf("\0"));
                        bool notMatch = true;
                        foreach (string n in strs)
                        {
                            if (n.Contains($"({pp})"))
                            {
                                notMatch = false;
                                break;
                            }
                        }
                        if (notMatch)
                            strs.Add($"Serial Port {pp} ({pp})");
                    }
                }
                catch { }

                Dispatcher.Invoke(() =>
                {
                    foreach (string i in strs)
                        serialPortsListComboBox.Items.Add(i);
                    if (strs.Count >= 1)
                    {
                        openClosePortButton.IsEnabled = true;
                        serialPortsListComboBox.SelectedIndex = 0;
                    }
                    else
                        openClosePortButton.IsEnabled = false;
                    refreshLock = false;

                    if (string.IsNullOrEmpty(lastPort))
                        lastPort = Tools.Global.uart.GetName();
                    foreach (string c in serialPortsListComboBox.Items)
                    {
                        if (c.Contains($"({lastPort})"))
                        {
                            serialPortsListComboBox.Text = c;
                            if (!forcusClosePort && Tools.Global.setting.autoReconnect && !isOpeningPort)
                            {
                                Task.Run(() =>
                                {
                                    isOpeningPort = true;
                                    try
                                    {
                                        Tools.Global.uart.Open();
                                        Dispatcher.Invoke(() =>
                                        {
                                            openClosePortTextBlock.Text = (TryFindResource("OpenPort_close") as string ?? "?!");
                                            serialPortsListComboBox.IsEnabled = false;
                                            SetMainWindowStatus(TryFindResource("OpenPort_open") as string ?? "?!");
                                        });
                                    }
                                    catch { }
                                    isOpeningPort = false;
                                });
                            }
                            break;
                        }
                    }
                });
            });
        }

        private void RefreshPortButton_Click(object sender, RoutedEventArgs e)
        {
            RefreshPortList();
        }

        private void OpenClosePortButton_Click(object sender, RoutedEventArgs e)
        {
            if (!Tools.Global.uart.IsOpen())
            {
                OpenPort();
            }
            else
            {
                string lastPort = null;
                try
                {
                    forcusClosePort = true;
                    lastPort = Tools.Global.uart.GetName();
                    Tools.Global.uart.Close();
                }
                catch
                {
                    Tools.MessageBox.Show(TryFindResource("ErrorClosePort") as string ?? "?!");
                }
                openClosePortTextBlock.Text = (TryFindResource("OpenPort_open") as string ?? "?!");
                serialPortsListComboBox.IsEnabled = true;
                SetMainWindowStatus(TryFindResource("OpenPort_close") as string ?? "?!");
                RefreshPortList(lastPort);
            }
        }

        private void OpenPort()
        {
            if (isOpeningPort)
                return;
            if (serialPortsListComboBox.SelectedItem != null)
            {
                string[] ports;
                try
                {
                    ports = SerialPort.GetPortNames();
                }
                catch
                {
                    ports = new string[0];
                }
                string port = "";
                foreach (string p in ports)
                {
                    var pp = p;
                    if (p.IndexOf("\0") > 0)
                        pp = p.Substring(0, p.IndexOf("\0"));
                    if ((serialPortsListComboBox.SelectedItem as string).Contains($"({pp})"))
                    {
                        port = pp;
                        break;
                    }
                }
                if (port != "")
                {
                    Task.Run(() =>
                    {
                        isOpeningPort = true;
                        try
                        {
                            forcusClosePort = false;
                            Tools.Global.uart.SetName(port);
                            Tools.Global.uart.Open();
                            Dispatcher.Invoke(() =>
                            {
                                openClosePortTextBlock.Text = (TryFindResource("OpenPort_close") as string ?? "?!");
                                serialPortsListComboBox.IsEnabled = false;
                                SetMainWindowStatus(TryFindResource("OpenPort_open") as string ?? "?!");
                            });
                            if (toSendData != null)
                            {
                                SendUartData(toSendData);
                                toSendData = null;
                            }
                        }
                        catch
                        {
                            Tools.MessageBox.Show(TryFindResource("ErrorOpenPort") as string ?? "?!");
                        }
                        isOpeningPort = false;
                    });
                }
            }
        }

        private void BaudRateComboBox_Changed(object sender, EventArgs e)
        {
            if (lastBaudRateSelectedIndex == baudRateComboBox.SelectedIndex)
                return;
            if (baudRateComboBox.SelectedItem != null)
            {
                lastBaudRateSelectedIndex = baudRateComboBox.SelectedIndex;
                if (baudRateComboBox.SelectedIndex == baudRateComboBox.Items.Count - 1)
                {
                    Tuple<bool, string> ret = Tools.InputDialog.OpenDialog(
                        TryFindResource("ShowBaudRate") as string ?? "?!",
                        "115200", TryFindResource("OtherRate") as string ?? "?!");
                    if (!ret.Item1 || !int.TryParse(ret.Item2, out int br))
                    {
                        Tools.MessageBox.Show(TryFindResource("OtherRateFail") as string ?? "?!");
                        return;
                    }
                    Tools.Global.setting.baudRate = br;
                    var text = Tools.Global.setting.baudRate.ToString();
                    baudRateComboBox.Items[baudRateComboBox.Items.Count - 1] = text;
                    baudRateComboBox.Text = text;
                }
                else
                {
                    Tools.Global.setting.baudRate =
                        int.Parse((baudRateComboBox.SelectedItem as ComboBoxItem).Content.ToString());
                    baudRateComboBox.Items[baudRateComboBox.Items.Count - 1] = TryFindResource("OtherRate") as string ?? "?!";
                }
            }
        }

        private void SendDataButton_Click(object sender, RoutedEventArgs e)
        {
            PerformSend();
        }

        /// <summary>
        /// 供 MainWindow Ctrl+Enter 及快捷发送调用
        /// </summary>
        public void PerformSend()
        {
            MainWindow.recvScriptBackup = Tools.Global.setting.recvScript;
            var data = Global.GetEncoding().GetBytes(toSendDataTextBox.Text);
            Global.recvPara = new byte[][] { new byte[0], data };
            SendUartData(data);
        }

        /// <summary>
        /// 发送串口数据，供快捷发送等调用
        /// </summary>
        public void SendUartData(byte[] data, bool? is_hex = null)
        {
            if (!Tools.Global.uart.IsOpen())
            {
                OpenPort();
                toSendData = (byte[])data.Clone();
                return;
            }
            if (Tools.Global.uart.IsOpen())
            {
                byte[] dataConvert;
                try
                {
                    dataConvert = LuaEnv.LuaLoader.Run(
                        $"{Tools.Global.setting.sendScript}.lua",
                        new System.Collections.ArrayList
                        {
                            "uartData",
                            is_hex == null ?
                            (Tools.Global.setting.hexSend ? Tools.Global.Hex2Byte(Tools.Global.Byte2String(data)) : data) : data
                        });
                }
                catch (Exception ex)
                {
                    Tools.MessageBox.Show($"{TryFindResource("ErrorScript") as string ?? "?!"}\r\n" + ex.ToString());
                    return;
                }
                try
                {
                    if (Tools.Global.setting.extraEnter)
                    {
                        var temp = new List<byte>(dataConvert) { 0x0d, 0x0a };
                        dataConvert = temp.ToArray();
                    }
                    Tools.Global.uart.SendData(dataConvert, data);
                }
                catch (Exception ex)
                {
                    Tools.MessageBox.Show($"{TryFindResource("ErrorSendFail") as string ?? "?!"}\r\n" + ex.ToString());
                }
            }
        }

        private void SetMainWindowStatus(string text)
        {
            var mw = Application.Current.MainWindow as MainWindow;
            mw?.SetSerialStatus(text);
        }
    }
}
