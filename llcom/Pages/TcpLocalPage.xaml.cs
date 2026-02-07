using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
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
using System.Threading;
using CoAP.Server;
using System.Diagnostics;
using static llcom.Pages.SocketClientPage;
using llcom.LuaEnv;
using System.Xml.Linq;

namespace llcom.Pages
{
    /// <summary>
    /// TcpLocalPage.xaml 的交互逻辑
    /// </summary>
    [PropertyChanged.AddINotifyPropertyChangedInterface]
    public partial class TcpLocalPage : Page, IDataInterfaceStatusProvider
    {
        public TcpLocalPage()
        {
            InitializeComponent();
        }

        //收到消息的事件
        public event EventHandler<byte[]> DataRecived;
        public bool IsConnected { get; set; } = false;

        public string GetStatusBarText()
        {
            if (!IsConnected)
                return "";
            var title = TryFindResource("TcpLocalTabTitle") as string ?? "TCP Server";
            return $"{title}：{IpPortTextBox.Text}";
        }

        private void NotifyStatusChanged()
        {
            Dispatcher.BeginInvoke(new Action(() =>
                (Application.Current.MainWindow as MainWindow)?.RefreshDataInterfaceStatus()));
        }

        private static bool loaded = false;
        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            if (loaded)
                return;
            loaded = true;
            RefreshIp();
            //绑定
            MainGrid.DataContext = this;
            IpPortTextBox.DataContext = Tools.Global.setting;
            var displayProxy = new Model.InterfaceConfigProxy("TcpLocal");
            OptionsScrollViewer.DataContext = displayProxy;
            toSendDataTextBox.DataContext = displayProxy;

            LoadSendScriptList();
            LoadRecvScriptList();

            //收到消息显示（与串口一致，recv_convert 在 DataShowPage 执行）
            DataRecived += (_, data) =>
            {
                Tools.Global.setting.ReceivedCount += data.Length;
                Tools.Logger.ShowData(data, false, "TcpLocal");
            };

            //适配一下通用通道
            LuaApis.SendChannelsRegister("tcp-server", (data, _) =>
            {
                if (Server != null && data != null)
                {
                    return Broadcast(data);
                }
                else
                    return false;
            });
            //通用通道收到消息
            DataRecived += (name, data) =>
            {
                LuaApis.SendChannelsReceived("tcp-server", 
                    new
                    {
                        from = (string)name,
                        data
                    });
            };
        }

        /// <summary>
        /// 刷新本机ip列表
        /// </summary>
        private void RefreshIp()
        {
            IpListComboBox.Items.Clear();
            IpListComboBox.Items.Add("0.0.0.0");
            IpListComboBox.Items.Add("::");
            var temp = new List<string>();
            try
            {
                string name = Dns.GetHostName();
                IPAddress[] ipadrlist = Dns.GetHostAddresses(name);
                foreach (IPAddress ipa in ipadrlist)
                {
                    if (ipa.AddressFamily == AddressFamily.InterNetwork ||
                        ipa.AddressFamily == AddressFamily.InterNetworkV6)
                        temp.Add(ipa.ToString());
                }
            }
            catch { }
            //去重
            temp.Distinct().ToList().ForEach(ip => IpListComboBox.Items.Add(ip));
            IpListComboBox.SelectedIndex = 0;
        }
        private void ShowData(string title, byte[] data = null, bool send = false)
        {
            Tools.Logger.ShowDataRaw(new Tools.DataShowRaw
            {
                title = $"🛰 local tcp server: {title}",
                data = data ?? new byte[0],
                color = send ? (Tools.Global.setting.darkMode ? Brushes.IndianRed : Brushes.DarkRed) : (Tools.Global.setting.darkMode ? Brushes.Lime : Brushes.DarkGreen),
            });
        }

        /// <summary>
        /// 获取客户端的名字
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        private string GetClientName(Socket s)
        {
            var remote = (IPEndPoint)s.RemoteEndPoint;
            var remoteIsV6 = remote.Address.ToString().Contains(":");
            var local = (IPEndPoint)s.LocalEndPoint;
            var localIsV6 = local.Address.ToString().Contains(":");
            return $"{(remoteIsV6 ? "[" : "")}{remote.Address}{(remoteIsV6 ? "]" : "")}:{remote.Port} → " +
                $"{(localIsV6 ? "[" : "")}{local.Address}{(localIsV6 ? "]" : "")}:{local.Port}";
        }


        private TcpListener Server = null;
        private List<Socket> Clients = new List<Socket>();


        public void Read_Callback(IAsyncResult ar)
        {
            StateObject so = (StateObject)ar.AsyncState;
            Socket s = so.workSocket;
            try
            {
                var name = GetClientName(s);
                int read = s.EndReceive(ar);

                if (read > 0)
                {
                    var buff = new byte[read];
                    for (int i = 0; i < buff.Length; i++)
                        buff[i] = so.buffer[i];
                    DataRecived?.Invoke(name, buff);
                    s.BeginReceive(so.buffer, 0, StateObject.BUFFER_SIZE, 0,
                                             new AsyncCallback(Read_Callback), so);
                }
            }
            catch//断了？
            {
                try
                {
                    var name = GetClientName(s);
                    lock (Clients)
                        Clients.Remove(s);
                    try
                    {
                        s.Close();
                        s.Dispose();
                    }
                    catch { }
                    ShowData($"☠ {name}");
                    LuaApis.SendChannelsReceived("tcp-server",
                        new
                        {
                            from = "disconnected",
                            data = name
                        });
                }
                catch { }
            }
        }

        /// <summary>
        /// 开始监听服务器
        /// </summary>
        /// <param name="ip"></param>
        /// <param name="port"></param>
        /// <returns></returns>
        private bool StartServer(string ip, int port)
        {
            if (Server != null)
                return false;
            IPAddress localAddr = IPAddress.Parse(ip);
            try
            {
                Server = new TcpListener(localAddr, port);
                Server.Start();
            }
            catch (Exception ex)
            {
                Server = null;
                throw ex;
            }
            
            var isV6 = ip.Contains(":");
            ShowData($"🛰 {(isV6 ? "[" : "")}{ip}{(isV6 ? "]" : "")}:{port}");
            AsyncCallback newConnectionCb = null;
            newConnectionCb = new AsyncCallback((ar) =>
            {
                TcpListener listener = (TcpListener)ar.AsyncState;
                try
                {
                    Socket client = listener.EndAcceptSocket(ar);//必须有这一句，不然新的请求没反应
                    ShowData($"😀 {GetClientName(client)}"); 
                    LuaApis.SendChannelsReceived("tcp-server",
                        new
                        {
                            from = "connected",
                            data = GetClientName(client)
                        });
                    lock (Clients)
                        Clients.Add(client);//加到列表里

                    //客户端数据接收回调
                    StateObject so = new StateObject();
                    so.workSocket = client;
                    client.BeginReceive(so.buffer, 0, StateObject.BUFFER_SIZE, 0, new AsyncCallback(Read_Callback), so);

                    //恢复服务端的回调函数，方便下次接收
                    Server.BeginAcceptSocket(newConnectionCb, Server);
                }
                catch { }
            });
            try
            {
                Server.BeginAcceptSocket(newConnectionCb, Server);
            }
            catch (Exception ex)
            {
                ShowData($"❗ Server create error {ex.Message}");
                return false;
            }

            return true;
        }

        /// <summary>
        /// 关闭服务器，断开所有连接
        /// </summary>
        private void StopServer()
        {
            lock (Clients)
            {
                foreach(var c in Clients)
                    try
                    {
                        var name = GetClientName(c);
                        c.Close();
                        c.Dispose();
                        ShowData($"☠ {name}");
                        LuaApis.SendChannelsReceived("tcp-server",
                            new
                            {
                                from = "disconnected",
                                data = name
                            });
                    }
                    catch { }
                Clients.Clear();
            }
            Server?.Stop();
            Server = null;
        }

        private void RefreshIpButton_Click(object sender, RoutedEventArgs e)
        {
            RefreshIp();
        }

        private void ListenButton_Click(object sender, RoutedEventArgs e)
        {
            int port;
            if(int.TryParse(IpPortTextBox.Text, out port))
            {
                try
                {
                    IsConnected = StartServer(IpListComboBox.Text, port);
                    if (IsConnected)
                        NotifyStatusChanged();
                }
                catch(Exception err)
                {
                    Tools.MessageBox.Show(err.Message);
                }
            }
        }

        private void StopListenButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                StopServer();
                IsConnected = false;
                ShowData($"🚫 server closed");
                NotifyStatusChanged();
            }
            catch { }
        }

        private bool sendScriptLoading = false;

        private void LoadSendScriptList()
        {
            sendScriptComboBox.Items.Clear();
            var dirPath = Tools.Global.ProfilePath + "user_script_send_convert/";
            if (!Directory.Exists(dirPath))
                Directory.CreateDirectory(dirPath);
            try
            {
                var dir = new DirectoryInfo(dirPath);
                foreach (var file in dir.GetFiles("*.lua"))
                {
                    var name = file.Name.Substring(0, file.Name.Length - 4);
                    sendScriptComboBox.Items.Add(name);
                }
            }
            catch { }
            var current = Tools.Global.setting.GetSendScriptForInterface("TcpLocal");
            sendScriptLoading = true;
            if (sendScriptComboBox.Items.Count > 0)
            {
                var found = false;
                for (int i = 0; i < sendScriptComboBox.Items.Count; i++)
                {
                    if ((sendScriptComboBox.Items[i] as string) == current)
                    {
                        sendScriptComboBox.SelectedIndex = i;
                        found = true;
                        break;
                    }
                }
                if (!found)
                {
                    Tools.Global.setting.SetSendScriptForInterface("TcpLocal", sendScriptComboBox.Items[0] as string ?? Tools.Global.GetDefaultScriptName());
                    sendScriptComboBox.SelectedIndex = 0;
                }
            }
            sendScriptLoading = false;
        }

        private void SendScriptComboBox_DropDownOpened(object sender, EventArgs e)
        {
            LoadSendScriptList();
        }

        private void SendScriptComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sendScriptLoading || sendScriptComboBox.SelectedItem == null) return;
            var name = sendScriptComboBox.SelectedItem as string;
            if (!string.IsNullOrEmpty(name) && name != Tools.Global.setting.GetSendScriptForInterface("TcpLocal"))
                Tools.Global.setting.SetSendScriptForInterface("TcpLocal", name);
        }

        private bool recvScriptLoading = false;
        private void LoadRecvScriptList()
        {
            recvScriptComboBox.Items.Clear();
            var dirPath = Tools.Global.ProfilePath + "user_script_recv_convert/";
            if (!Directory.Exists(dirPath))
                Directory.CreateDirectory(dirPath);
            try
            {
                var dir = new DirectoryInfo(dirPath);
                foreach (var file in dir.GetFiles("*.lua"))
                {
                    var name = file.Name.Substring(0, file.Name.Length - 4);
                    recvScriptComboBox.Items.Add(name);
                }
            }
            catch { }
            var current = Tools.Global.setting.GetRecvScriptForInterface("TcpLocal");
            recvScriptLoading = true;
            if (recvScriptComboBox.Items.Count > 0)
            {
                var found = false;
                for (int i = 0; i < recvScriptComboBox.Items.Count; i++)
                {
                    if ((recvScriptComboBox.Items[i] as string) == current)
                    {
                        recvScriptComboBox.SelectedIndex = i;
                        found = true;
                        break;
                    }
                }
                if (!found)
                {
                    Tools.Global.setting.SetRecvScriptForInterface("TcpLocal", recvScriptComboBox.Items[0] as string ?? Tools.Global.GetDefaultScriptName());
                    recvScriptComboBox.SelectedIndex = 0;
                }
            }
            recvScriptLoading = false;
        }

        private void RecvScriptComboBox_DropDownOpened(object sender, EventArgs e)
        {
            LoadRecvScriptList();
        }

        private void RecvScriptComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (recvScriptLoading || recvScriptComboBox.SelectedItem == null) return;
            var name = recvScriptComboBox.SelectedItem as string;
            if (!string.IsNullOrEmpty(name) && name != Tools.Global.setting.GetRecvScriptForInterface("TcpLocal"))
                Tools.Global.setting.SetRecvScriptForInterface("TcpLocal", name);
        }

        private void SendDataButton_Click(object sender, RoutedEventArgs e)
        {
            if (Server != null)
            {
                var text = Tools.Global.setting.GetDataToSendForInterface("TcpLocal") ?? "";
                var buff = Tools.Global.GetEncoding().GetBytes(text);
                Tools.Global.recvPara = new byte[][] { new byte[0], buff };
                Broadcast(buff);
            }
        }

        private bool Broadcast(byte[] buff)
        {
            if (buff == null || buff.Length == 0)
                return false;
            if (Tools.Global.recvPara == null)
                Tools.Global.recvPara = new byte[][] { new byte[0], buff };
            var toSend = Tools.LuaConvertHelper.ApplySendConvert(buff, "TcpLocal");
            if (toSend == null)
                return false;
            try
            {
                lock (Clients)
                {
                    foreach (var c in Clients)
                        try { c.Send(toSend); } catch { }
                }
                Tools.Global.setting.SentCount += toSend.Length;
                bool showRaw = buff != null && Tools.Global.setting.GetShowSendRawForInterface("TcpLocal");
                bool showConverted = Tools.Global.setting.GetShowSendForInterface("TcpLocal");
                if (showRaw && showConverted && buff != null && toSend.SequenceEqual(buff))
                    Tools.Logger.ShowData(toSend, true, "TcpLocal");
                else
                {
                    if (showRaw && buff != null) Tools.Logger.ShowData(buff, true, "TcpLocal");
                    if (showConverted) Tools.Logger.ShowData(toSend, true, "TcpLocal");
                }
                return true;
            }
            catch (Exception ex)
            {
                ShowData($"❗ broadcast error {ex.Message}");
                return false;
            }
        }
    }
}
