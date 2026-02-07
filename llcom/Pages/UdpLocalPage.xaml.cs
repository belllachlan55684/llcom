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
using static llcom.Pages.SocketClientPage;
using System.Net.NetworkInformation;
using llcom.LuaEnv;

namespace llcom.Pages
{
    /// <summary>
    /// UdpLocalPage.xaml 的交互逻辑
    /// </summary>
    [PropertyChanged.AddINotifyPropertyChangedInterface]
    public partial class UdpLocalPage : Page, IDataInterfaceStatusProvider
    {
        public UdpLocalPage()
        {
            InitializeComponent();
        }


        public bool IsConnected { get; set; } = false;

        private IPEndPoint lastRemoteEndPoint = null;
        private bool sendScriptLoading = false;

        public string GetStatusBarText()
        {
            if (!IsConnected)
                return "";
            var title = TryFindResource("UdpLocalTabTitle") as string ?? "UDP Server";
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
            OptionsScrollViewer.DataContext = Tools.Global.setting;
            toSendDataTextBox.DataContext = Tools.Global.setting;

            LoadSendScriptList();

            LuaApis.SendChannelsRegister("udp-server", (data, _) =>
            {
                if (Server != null && data != null && lastRemoteEndPoint != null)
                {
                    return SendToClient(lastRemoteEndPoint, data);
                }
                return false;
            });
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
                title = $"🗑 local udp server: {title}",
                data = data ?? new byte[0],
                color = send ? (Tools.Global.setting.darkMode ? Brushes.IndianRed : Brushes.DarkRed) : (Tools.Global.setting.darkMode ? Brushes.Lime : Brushes.DarkGreen),
            });
        }


        private UdpClient Server = null;
        private readonly object lastRemoteLock = new object();

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
            IPEndPoint IpEndPoint = new IPEndPoint(localAddr, port);
            Server = new UdpClient(IpEndPoint);

            var isV6 = ip.Contains(":");
            ShowData($"🗑 {(isV6 ? "[" : "")}{ip}{(isV6 ? "]" : "")}:{port}");
            NotifyStatusChanged();

            AsyncCallback newConnectionCb = null;
            newConnectionCb = new AsyncCallback((ar) =>
            {
                try
                {
                    UdpClient u = ((UdpState)(ar.AsyncState)).u;
                    IPEndPoint remoteEp = ((UdpState)(ar.AsyncState)).e;

                    byte[] receiveBytes = u.EndReceive(ar, ref remoteEp);
                    lock (lastRemoteLock)
                        lastRemoteEndPoint = remoteEp;
                    Tools.Global.setting.ReceivedCount += receiveBytes.Length;
                    Tools.Logger.ShowData(receiveBytes, false);
                    Server.BeginReceive(newConnectionCb, new UdpState { u = Server, e = remoteEp });
                }
                catch { }
            }); 
            UdpState s = new UdpState();
            s.e = IpEndPoint;
            s.u = Server;
            try
            {
                Server.BeginReceive(newConnectionCb, s);
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
            Server?.Close();
            Server?.Dispose();
            Server = null;
            lock (lastRemoteLock)
                lastRemoteEndPoint = null;
            IsConnected = false;
            NotifyStatusChanged();
        }

        private void RefreshIpButton_Click(object sender, RoutedEventArgs e)
        {
            RefreshIp();
        }

        private void ListenButton_Click(object sender, RoutedEventArgs e)
        {
            int port;
            if (int.TryParse(IpPortTextBox.Text, out port))
            {
                try
                {
                    IsConnected = StartServer(IpListComboBox.Text, port);
                    if (IsConnected)
                        NotifyStatusChanged();
                }
                catch (Exception err)
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
            }
            catch { }
        }

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
            var current = Tools.Global.setting.GetSendScriptForInterface("UdpLocal");
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
                    Tools.Global.setting.SetSendScriptForInterface("UdpLocal", sendScriptComboBox.Items[0] as string ?? Tools.Global.GetDefaultScriptName());
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
            if (!string.IsNullOrEmpty(name) && name != Tools.Global.setting.GetSendScriptForInterface("UdpLocal"))
                Tools.Global.setting.SetSendScriptForInterface("UdpLocal", name);
        }

        private void SendDataButton_Click(object sender, RoutedEventArgs e)
        {
            if (Server == null) return;
            IPEndPoint target;
            lock (lastRemoteLock)
                target = lastRemoteEndPoint;
            if (target == null)
            {
                Tools.MessageBox.Show(TryFindResource("UdpNoClientTip") as string ?? "No client has sent data yet. Send data to the server first.");
                return;
            }
            var text = Tools.Global.setting.dataToSend ?? "";
            var buff = Tools.Global.GetEncoding().GetBytes(text);
            MainWindow.recvScriptBackup = Tools.Global.setting.recvScript;
            Tools.Global.recvPara = new byte[][] { new byte[0], buff };
            SendToClient(target, buff);
        }

        private bool SendToClient(IPEndPoint target, byte[] buff)
        {
            if (buff == null || buff.Length == 0 || target == null || Server == null)
                return false;
            var toSend = Tools.LuaConvertHelper.ApplySendConvert(buff, "UdpLocal");
            if (toSend == null)
                return false;
            try
            {
                Server.Send(toSend, toSend.Length, target);
                Tools.Global.setting.SentCount += toSend.Length;
                bool showRaw = buff != null && Tools.Global.setting.showSendRaw;
                bool showConverted = Tools.Global.setting.showSend;
                if (showRaw && showConverted && buff != null && toSend.SequenceEqual(buff))
                    Tools.Logger.ShowData(toSend, true);
                else
                {
                    if (showRaw && buff != null) Tools.Logger.ShowData(buff, true);
                    if (showConverted) Tools.Logger.ShowData(toSend, true);
                }
                return true;
            }
            catch (Exception ex)
            {
                ShowData($"❗ send error {ex.Message}");
                return false;
            }
        }
    }

    public struct UdpState
    {
        public UdpClient u;
        public IPEndPoint e;
    }
}
