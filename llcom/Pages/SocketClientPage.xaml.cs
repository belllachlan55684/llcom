using llcom.LuaEnv;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using static llcom.Pages.SocketClientPage;

namespace llcom.Pages
{
    /// <summary>
    /// SocketClientPage.xaml 的交互逻辑
    /// </summary>
    [PropertyChanged.AddINotifyPropertyChangedInterface]
    public partial class SocketClientPage : Page, IDataInterfaceStatusProvider, IQuickSendTarget
    {
        public SocketClientPage()
        {
            InitializeComponent();
        }
        private bool initial = false;
        //收到消息的事件
        public event EventHandler<byte[]> DataRecived;
        public bool IsConnected { get; set; } = false;
        public bool NeedDisconnected { get; set; } = false;

        //是否可更改服务器信息
        public bool Changeable { get; set; } = true;

        //暂存一个对象
        SocketObj socketNow = null;

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            if (initial)
                return;
            initial = true;

            this.DataContext = this;

            ConfigWrapPanel.DataContext = Tools.Global.setting;
            ReconnectInterval.DataContext = Tools.Global.setting;
            var displayProxy = new Model.InterfaceConfigProxy("TcpClient");
            OptionsScrollViewer.DataContext = displayProxy;
            toSendDataTextBox.DataContext = displayProxy;
            SendDockPanel.DataContext = displayProxy;

            //收到消息显示（与串口一致，recv_convert 在 DataShowPage 执行）
            DataRecived += (_, buff) =>
            {
                Tools.Global.setting.ReceivedCount += buff.Length;
                Tools.Logger.ShowData(buff, false, "TcpClient");
            };

            //适配一下通用通道
            LuaApis.SendChannelsRegister("socket-client", (data, _) =>
            {
                if (socketNow != null && data != null)
                {
                    return Send(data);
                }
                else
                    return false;
            });
            //通用通道收到消息
            DataRecived += (_, data) =>
            {
                LuaApis.SendChannelsReceived("socket-client", data);
            };
        }

        public string GetStatusBarText()
        {
            if (!IsConnected)
                return "";
            var protocol = ProtocolTypeComboBox.SelectedIndex switch { 0 => "TCP", 1 => "UDP", 2 => "TCP SSL", _ => "TCP" };
            var addr = ServerTextBox?.Text?.Trim() ?? "";
            var port = PortTextBox?.Text?.Trim() ?? "";
            return string.IsNullOrEmpty(addr) || string.IsNullOrEmpty(port) ? $"{protocol}：{port}" : $"{protocol} {addr}：{port}";
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
                title = $"🔗 socket client: {title}",
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
                s = new Socket(ipe.AddressFamily,
                    ProtocolTypeComboBox.SelectedIndex == 1 ? SocketType.Dgram : SocketType.Stream,
                    ProtocolTypeComboBox.SelectedIndex == 1 ? ProtocolType.Udp : ProtocolType.Tcp);
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
                StateObject so = new StateObject();
                so.isSSL = ProtocolTypeComboBox.SelectedIndex == 2;
                s.BeginConnect(ipe, new AsyncCallback((r) =>
                {
                    var s = (Socket)r.AsyncState;
                    if (s.Connected)
                    {
                        if (!so.isSSL)
                            socketNow = new SocketObj(s);
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

                    if (so.isSSL)
                    {
                        var networkStream = new NetworkStream(s);
                        var ssl = new SslStream(
                            networkStream,
                               false,
                                new RemoteCertificateValidationCallback((_, _, _, _) => true),
                                null);
                        so.workStream = ssl;
                        try
                        {
                            ssl.AuthenticateAsClient("llcom tcp ssl client");
                        }
                        catch (Exception ssle)
                        {
                            ShowData($"❗ SSL error {ssle.Message}");
                            socketNow = null;
                            IsConnected = false;
                            if (!Tools.Global.setting.tcpReconnect)
                                NeedDisconnected = false;
                            Changeable = true;
                            s.Close();
                            NotifyStatusChanged();
                            s.Dispose();
                            ShowData("❌ Server disconnected");
                            return;
                        }
                        try
                        {
                            socketNow = new SocketObj(ssl);
                            ssl.BeginRead(so.buffer, 0, StateObject.BUFFER_SIZE, new AsyncCallback(Read_Callback), so);
                            NotifyStatusChanged();
                        }
                        catch (Exception ex)
                        {
                            ShowData($"❗ Server connect error {ex.Message}");
                            socketNow = null;
                            IsConnected = false;
                            Changeable = true;
                            s.Close();
                            s.Dispose();
                            ShowData("❌ Server disconnected");
                            NotifyStatusChanged();
                            return;
                        }
                    }
                    else
                    {
                        so.workSocket = s;
                        try
                        {
                            s.BeginReceive(so.buffer, 0, StateObject.BUFFER_SIZE, 0, new AsyncCallback(Read_Callback), so);
                            NotifyStatusChanged();
                        }
                        catch(Exception ex)
                        {
                            ShowData($"❗ Server connect error {ex.Message}");
                            socketNow = null;
                            IsConnected = false;
                            Changeable = true;
                            s.Close();
                            s.Dispose();
                            ShowData("❌ Server disconnected");
                            return;
                        }
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
            if (Tools.Global.setting.tcpReconnect)
            {
                reconnectTimer = new System.Timers.Timer(Tools.Global.setting.tcpReconnectInterval * 1000);
                reconnectTimer.Elapsed += (_, _) =>
                {
                    if (!IsConnected)
                    {
                        Dispatcher.Invoke(() =>
                        {
                            Reconnect();
                        });
                    }
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
            StateObject so = (StateObject)ar.AsyncState;

            if (so.isSSL)//ssl连接
            {
                var ssl = so.workStream;
                try
                {
                    int read = ssl.EndRead(ar);

                    if (read > 0)
                    {
                        var buff = new byte[read];
                        for (int i = 0; i < buff.Length; i++)
                            buff[i] = so.buffer[i];
                        DataRecived?.Invoke(null, buff);
                        ssl.BeginRead(so.buffer, 0, StateObject.BUFFER_SIZE,
                                                 new AsyncCallback(Read_Callback), so);
                    }
                    else//断了？
                    {
                        try
                        {
                            ssl.Close();
                            ssl.Dispose();
                        }
                        catch { }
                        socketNow = null;
                        IsConnected = false;
                        if (!Tools.Global.setting.tcpReconnect)
                            NeedDisconnected = false;
                        Changeable = true;
                        ShowData("❌ Server disconnected");
                        NotifyStatusChanged();
                    }
                }
                catch { }

                return;
            }

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
                    s.BeginReceive(so.buffer, 0, StateObject.BUFFER_SIZE, 0,
                                             new AsyncCallback(Read_Callback), so);
                }
                else//断了？
                {
                    try
                    {
                        s.Close();
                        s.Dispose();
                    }
                    catch { }
                    socketNow = null;
                    IsConnected = false;
                    if (!Tools.Global.setting.tcpReconnect)
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
            if(socketNow != null)
            {
                try
                {
                    socketNow.Close();
                }
                catch { }
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
                var text = Tools.Global.setting.GetDataToSendForInterface("TcpClient") ?? "";
                var isHex = Tools.Global.setting.GetHexForInterface("TcpClient");
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
            var toSend = Tools.LuaConvertHelper.ApplySendConvert(buff, "TcpClient");
            if (toSend == null)
                return false;
            try
            {
                socketNow.Send(toSend);
                Tools.Global.setting.SentCount += toSend.Length;
                bool showRaw = buff != null && Tools.Global.setting.GetShowSendRawForInterface("TcpClient");
                bool showConverted = Tools.Global.setting.GetShowSendForInterface("TcpClient");
                if (showRaw && showConverted && buff != null && toSend.SequenceEqual(buff))
                    Tools.Logger.ShowData(toSend, true, "TcpClient");
                else
                {
                    if (showRaw && buff != null) Tools.Logger.ShowData(buff, true, "TcpClient");
                    if (showConverted) Tools.Logger.ShowData(toSend, true, "TcpClient");
                }
                return true;
            }
            catch (Exception ex)
            {
                ShowData($"❗ Send data error {ex.Message}");
                return false;
            }
        }

        public class StateObject
        {
            public Socket workSocket = null;
            public SslStream workStream = null;
            public const int BUFFER_SIZE = 204800;
            public byte[] buffer = new byte[BUFFER_SIZE];
            public bool isSSL = false;
        }

        public class SocketObj
        {
            Socket socket;
            SslStream sslStream;
            public SocketObj(Socket s)
            {
                socket = s;
            }
            public SocketObj(SslStream ssl)
            {
                sslStream = ssl;
            }
            public void Send(byte[] buff)
            {
                if (socket != null)
                    socket.Send(buff);
                else if (sslStream != null)
                {
                    sslStream.Write(buff);
                }
                    
            }

            public void Close()
            {
                if (socket != null)
                {
                    socket.Close();
                    socket.Dispose();
                }
                else if (sslStream != null)
                {
                    sslStream.Close();
                    sslStream.Dispose();
                }
            }
        }
    }
}
