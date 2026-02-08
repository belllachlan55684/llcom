using llcom.LuaEnv;
using llcom.Model;
using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace llcom.Pages
{
    [PropertyChanged.AddINotifyPropertyChangedInterface]
    public partial class UdpClientPage : Page, IDataInterfaceStatusProvider, IQuickSendTarget
    {
        public UdpClientPage()
        {
            InitializeComponent();
        }
        private bool initial = false;
        public event EventHandler<byte[]> DataRecived;
        public bool IsConnected { get; set; } = false;
        public bool NeedDisconnected { get; set; } = false;
        public bool Changeable { get; set; } = true;

        SocketObj socketNow = null;

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            if (initial)
                return;
            initial = true;

            this.DataContext = this;
            ConfigWrapPanel.DataContext = Tools.Global.setting;
            ReconnectInterval.DataContext = Tools.Global.setting;
            var displayProxy = new Model.InterfaceConfigProxy("UdpClient");
            OptionsScrollViewer.DataContext = displayProxy;
            toSendDataTextBox.DataContext = displayProxy;
            SendDockPanel.DataContext = displayProxy;

            DataRecived += (_, buff) =>
            {
                Tools.Global.setting.ReceivedCount += buff.Length;
                Tools.Logger.ShowData(buff, false, "UdpClient");
            };

            LuaApis.SendChannelsRegister("udp-client", (data, _) =>
            {
                if (socketNow != null && data != null)
                    return Send(data);
                return false;
            });
            DataRecived += (_, data) =>
            {
                LuaApis.SendChannelsReceived("udp-client", data);
            };
        }

        public string GetStatusBarText()
        {
            if (!IsConnected)
                return "";
            var addr = ServerTextBox?.Text?.Trim() ?? "";
            var port = PortTextBox?.Text?.Trim() ?? "";
            return string.IsNullOrEmpty(addr) || string.IsNullOrEmpty(port) ? $"UDP：{port}" : $"UDP {addr}：{port}";
        }

        private void NotifyStatusChanged()
        {
            Dispatcher.BeginInvoke(new Action(() =>
                (Application.Current.MainWindow as MainWindow)?.RefreshDataInterfaceStatus()));
        }

        private void ShowData(string title, byte[] data = null, bool send = false)
        {
            Tools.Logger.ShowDataRaw(new Tools.DataShowRaw
            {
                title = $"🔗 udp client: {title}",
                data = data ?? new byte[0],
                color = send ? (Tools.Global.setting.darkMode ? Brushes.IndianRed : Brushes.DarkRed) : (Tools.Global.setting.darkMode ? Brushes.Lime : Brushes.DarkGreen),
            });
        }

        private System.Timers.Timer reconnectTimer = null;

        private void Reconnect()
        {
            if (!Changeable || IsConnected)
                return;

            IPEndPoint ipe = null;
            Socket s = null;
            try
            {
                Changeable = false;
                IPAddress ip = null;
                try
                {
                    ip = IPAddress.Parse(ServerTextBox.Text);
                }
                catch
                {
                    var hostEntry = Dns.GetHostEntry(ServerTextBox.Text);
                    ip = hostEntry.AddressList[0];
                }
                ipe = new IPEndPoint(ip, int.Parse(PortTextBox.Text));
                s = new Socket(ipe.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
            }
            catch (Exception ex)
            {
                ShowData($"❗ Server information error {ex.Message}");
                Changeable = true;
                return;
            }
            ShowData("📢 Connecting......");
            try
            {
                var so = new StateObject();
                so.isSSL = false;
                s.BeginConnect(ipe, new AsyncCallback((r) =>
                {
                    var sock = (Socket)r.AsyncState;
                    if (sock.Connected)
                    {
                        socketNow = new SocketObj(sock);
                        IsConnected = true;
                        NeedDisconnected = true;
                        ShowData("✔ Server connected");
                        NotifyStatusChanged();
                    }
                    else
                    {
                        Changeable = true;
                        ShowData("❗ Server connect failed");
                        return;
                    }
                    so.workSocket = sock;
                    try
                    {
                        sock.BeginReceive(so.buffer, 0, StateObject.BUFFER_SIZE, 0, new AsyncCallback(Read_Callback), so);
                        NotifyStatusChanged();
                    }
                    catch (Exception ex)
                    {
                        ShowData($"❗ Server connect error {ex.Message}");
                        socketNow = null;
                        IsConnected = false;
                        Changeable = true;
                        sock.Close();
                        sock.Dispose();
                        ShowData("❌ Server disconnected");
                        NotifyStatusChanged();
                    }
                }), s);
            }
            catch (Exception ex)
            {
                ShowData($"❗ Server connect error {ex.Message}");
                Changeable = true;
                return;
            }
        }

        private void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            if (Tools.Global.setting.udpClientReconnect)
            {
                reconnectTimer = new System.Timers.Timer(Tools.Global.setting.udpClientReconnectInterval * 1000);
                reconnectTimer.Elapsed += (_, _) =>
                {
                    if (!IsConnected)
                        Dispatcher.Invoke(() => Reconnect());
                };
                reconnectTimer.AutoReset = true;
                reconnectTimer.Enabled = true;
                NeedDisconnected = true;
            }
            else
            {
                if (reconnectTimer != null)
                {
                    reconnectTimer.Stop();
                    reconnectTimer.Dispose();
                    reconnectTimer = null;
                }
            }
            Reconnect();
        }

        public void Read_Callback(IAsyncResult ar)
        {
            var so = (StateObject)ar.AsyncState;
            Socket s = so.workSocket;
            try
            {
                int read = s.EndReceive(ar);
                if (read > 0)
                {
                    var buff = new byte[read];
                    for (int i = 0; i < buff.Length; i++)
                        buff[i] = so.buffer[i];
                    DataRecived?.Invoke(null, buff);
                    s.BeginReceive(so.buffer, 0, StateObject.BUFFER_SIZE, 0, new AsyncCallback(Read_Callback), so);
                }
                else
                {
                    try { s.Close(); s.Dispose(); } catch { }
                    socketNow = null;
                    IsConnected = false;
                    if (!Tools.Global.setting.udpClientReconnect)
                        NeedDisconnected = false;
                    Changeable = true;
                    ShowData("❌ Server disconnected");
                    NotifyStatusChanged();
                }
            }
            catch { }
        }

        private void Reconnect_TextInputCheck(object sender, TextCompositionEventArgs e)
        {
            if (string.IsNullOrEmpty(e.Text)) return;
            if (!e.Text.All(char.IsDigit))
            {
                e.Handled = true;
                return;
            }
            var tb = sender as TextBox;
            if (tb == null) return;
            var newText = tb.Text.Substring(0, tb.SelectionStart) + e.Text + tb.Text.Substring(tb.SelectionStart + tb.SelectionLength);
            if (!string.IsNullOrEmpty(newText) && (!int.TryParse(newText, out int num) || num < 0 || num > 120))
                e.Handled = true;
        }

        private void DisconnectButton_Click(object sender, RoutedEventArgs e)
        {
            if (socketNow != null)
            {
                try { socketNow.Close(); } catch { }
                socketNow = null;
                IsConnected = false;
                Changeable = true;
                ShowData("❌ Server disconnected");
                NotifyStatusChanged();
            }
            NeedDisconnected = false;
            if (reconnectTimer != null)
            {
                reconnectTimer.Stop();
                reconnectTimer.Dispose();
                reconnectTimer = null;
            }
        }

        private void SendButton_Click(object sender, RoutedEventArgs e)
        {
            if (socketNow != null)
            {
                var text = Tools.Global.setting.GetDataToSendForInterface("UdpClient") ?? "";
                var isHex = Tools.Global.setting.GetHexForInterface("UdpClient");
                PerformSendWithData(text, isHex);
            }
        }

        public bool PerformSendWithData(string text, bool isHex)
        {
            if (socketNow == null) return false;
            byte[] buff = isHex ? Tools.Global.Hex2Byte(text) : Tools.Global.GetEncoding().GetBytes(text);
            if (buff == null || buff.Length == 0) return false;
            Tools.Global.recvPara = new byte[][] { new byte[0], buff };
            return Send(buff);
        }

        private bool Send(byte[] buff)
        {
            if (buff == null || buff.Length == 0)
                return false;
            if (Tools.Global.recvPara == null)
                Tools.Global.recvPara = new byte[][] { new byte[0], buff };
            var toSend = Tools.LuaConvertHelper.ApplySendConvert(buff, "UdpClient");
            if (toSend == null)
                return false;
            try
            {
                socketNow.Send(toSend);
                Tools.Global.setting.SentCount += toSend.Length;
                bool showRaw = buff != null && Tools.Global.setting.GetShowSendRawForInterface("UdpClient");
                bool showConverted = Tools.Global.setting.GetShowSendForInterface("UdpClient");
                if (showRaw && showConverted && buff != null && toSend.SequenceEqual(buff))
                    Tools.Logger.ShowData(toSend, true, "UdpClient");
                else
                {
                    if (showRaw && buff != null) Tools.Logger.ShowData(buff, true, "UdpClient");
                    if (showConverted) Tools.Logger.ShowData(toSend, true, "UdpClient");
                }
                return true;
            }
            catch (Exception ex)
            {
                ShowData($"❗ Send data error {ex.Message}");
                return false;
            }
        }
    }
}
