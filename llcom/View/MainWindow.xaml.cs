using FontAwesome.WPF;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using ICSharpCode.AvalonEdit.Search;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Management;
using System.Net.Http;
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
using System.Xml;
using llcom.Model;
using System.Text.RegularExpressions;
using llcom.Tools;
using ICSharpCode.AvalonEdit.Folding;
using RestSharp;
using System.Threading;
using System.Windows.Interop;
using System.Drawing;
using ICSharpCode.AvalonEdit;
using System.Runtime.InteropServices;
using System.Windows.Controls.Primitives;
using llcom.LuaEnv;
using System.Web.UI.WebControls.WebParts;
using Color = System.Windows.Media.Color;

namespace llcom
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            Tools.Global.LoadSetting();
            Tools.Global.Initial();
            InitializeComponent();

            // SystemCommands 用于自定义标题栏的最小化/关闭
            CommandBindings.Add(new CommandBinding(
                SystemCommands.MinimizeWindowCommand,
                (s, e) => SystemCommands.MinimizeWindow(this)));
            CommandBindings.Add(new CommandBinding(
                SystemCommands.CloseWindowCommand,
                (s, e) => SystemCommands.CloseWindow(this)));

            StateChanged += MainWindow_StateChanged;

            if (Tools.Global.setting.windowHeight != 0 &&
                Tools.Global.setting.windowLeft > 0 &&
                Tools.Global.setting.windowTop > 0 &&
                Tools.Global.setting.windowTop < SystemParameters.FullPrimaryScreenHeight &&
                Tools.Global.setting.windowLeft < SystemParameters.FullPrimaryScreenWidth)
            {
                this.Left = Tools.Global.setting.windowLeft;
                this.Top = Tools.Global.setting.windowTop;
                this.Width = Tools.Global.setting.windowWidth;
                this.Height = Tools.Global.setting.windowHeight;
            }
        }
        ObservableCollection<ToSendData> toSendListItems = new ObservableCollection<ToSendData>();
        private bool canSaveSendList = true;
        public static string recvScriptBackup = "";
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            //延迟启动，加快软件第一屏出现速度
            Task.Run(() =>
            {
                this.Dispatcher.Invoke(new Action(delegate {
                    //接收到、发送数据成功回调
                    Tools.Global.uart.UartDataRecived += Uart_UartDataRecived;
                    Tools.Global.uart.UartDataSent += Uart_UartDataSent;

                    //初始化所有数据
                    Tools.Global.Initial();

                    //重写关闭窗口代码
                    this.Closing += MainWindow_Closing;

                    //窗口置顶事件
                    Tools.Global.setting.MainWindowTop += new EventHandler(topEvent);
                    if (Tools.Global.setting.topmost)//设置窗口置顶
                        this.Topmost = true;

                    //收发数据显示页面
                    dataShowFrame.Navigate(new Uri("Pages/DataShowPage.xaml", UriKind.Relative));

                    //串口选项卡（波特率初始化、刷新设备列表在 SerialPortPage.Page_Loaded 中）
                    SerialPortFrame.Navigate(new Uri("Pages/SerialPortPage.xaml", UriKind.Relative));

                    // 绑定事件监听,用于监听HID设备插拔
                    (PresentationSource.FromVisual(this) as HwndSource)?.AddHook(WndProc);

                    // 订阅 SentCount/ReceivedCount 变化以刷新串口状态栏（PropertyChanged 在后台线程触发，必须通过 Dispatcher 切回 UI 线程）
                    if (Tools.Global.setting is System.ComponentModel.INotifyPropertyChanged inpc)
                    {
                        inpc.PropertyChanged += (s, ev) =>
                        {
                            if (ev.PropertyName == "SentCount" || ev.PropertyName == "ReceivedCount")
                                Dispatcher.BeginInvoke(new Action(RefreshDataInterfaceStatus));
                        };
                    }

                    //绑定数据
                    toSendList.ItemsSource = toSendListItems;
                    QuiclListName0.DataContext = Tools.Global.setting;
                    QuiclListName1.DataContext = Tools.Global.setting;
                    QuiclListName2.DataContext = Tools.Global.setting;
                    QuiclListName3.DataContext = Tools.Global.setting;
                    QuiclListName4.DataContext = Tools.Global.setting;
                    QuiclListName5.DataContext = Tools.Global.setting;
                    QuiclListName6.DataContext = Tools.Global.setting;
                    QuiclListName7.DataContext = Tools.Global.setting;
                    QuiclListName8.DataContext = Tools.Global.setting;
                    QuiclListName9.DataContext = Tools.Global.setting;

                    //初始化快捷发送栏的数据
                    canSaveSendList = false;
                    if (Global.setting.quickSendSelect == -1)
                        Global.setting.quickSendSelect = 0;
                    ToSendData.DataChanged += SaveSendList;
                    LoadQuickSendList();
                    canSaveSendList = true;


                    //快速搜索
                    SearchPanel.Install(textEditor.TextArea);

                    var foldingManager = FoldingManager.Install(textEditor.TextArea);
                    var foldingStrategy = new Model.LuaFolding();

                    Task.Run(() =>
                    {
                        while (true)
                        {
                            Task.Delay(1000).Wait();
                            this.Dispatcher.Invoke(new Action(delegate
                            {
                                try
                                {
                                    foldingStrategy.UpdateFoldings(foldingManager, textEditor.Document);
                                }
                                catch { }
                            }));
                        }
                    });

                    ApplyScriptEditorTheme(Tools.Global.setting.darkMode);
                    Tools.Global.ThemeChanged += (_, dark) => Dispatcher.Invoke(() => ApplyScriptEditorTheme(dark));

                    //加载上次打开的文件
                    loadLuaFile(Tools.Global.setting.runScript);

                    //加载lua日志打印事件
                    LuaEnv.LuaApis.PrintLuaLog += LuaApis_PrintLuaLog;
                    //lua代码出错/结束运行事件
                    LuaEnv.LuaRunEnv.LuaRunError += LuaRunEnv_LuaRunError;

                    //在线脚本列表
                    OnlineScriptsFrame.Navigate(new Uri("Pages/OnlineScriptsPage.xaml", UriKind.Relative));

                    //关于页面
                    aboutFrame.Navigate(new Uri("Pages/AboutPage.xaml", UriKind.Relative));

                    //tcp测试页面
                    tcpTestFrame.Navigate(new Uri("Pages/tcpTest.xaml", UriKind.Relative));

                    //tcp客户端页面
                    tcpClientFrame.Navigate(new Uri("Pages/SocketClientPage.xaml", UriKind.Relative));

                    //本地tcp服务器
                    tcpLocalTestFrame.Navigate(new Uri("Pages/TcpLocalPage.xaml", UriKind.Relative));

                    //本地udp服务器
                    udpLocalTestFrame.Navigate(new Uri("Pages/UdpLocalPage.xaml", UriKind.Relative));

                    //mqtt测试页面
                    MqttTestFrame.Navigate(new Uri("Pages/MqttTestPage.xaml", UriKind.Relative));

                    //编码转换工具页面
                    EncodingToolsFrame.Navigate(new Uri("Pages/ConvertPage.xaml", UriKind.Relative));

                    //乱码修复
                    EncodingFixFrame.Navigate(new Uri("Pages/EncodingFixPage.xaml", UriKind.Relative));

                    //串口监听
                    SerialMonitorFrame.Navigate(new Uri("Pages/SerialMonitorPage.xaml", UriKind.Relative));

                    //绘制曲线
                    PlotFrame.Navigate(new Uri("Pages/PlotPage.xaml", UriKind.Relative));

                    //WinUSB
                    WinUSBFrame.Navigate(new Uri("Pages/WinUSBPage.xaml", UriKind.Relative));

                    this.Title += $" - {System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString()}";

                    TongjiWebBrowser.Source = new Uri(
                            $"https://llcom.papapoi.com/tongji.html?{System.Reflection.Assembly.GetExecutingAssembly().GetName().Version}"
                        );

                    new Thread(LuaLogPrintTask).Start();

                    //加载完了，可以允许点击
                    MainGrid.IsEnabled = true;

                    RefreshDataInterfaceStatus();

                    //检查更新
                    if (!Tools.Global.IsMSIX())
                    {
                        Task.Run(() => {
                            bool runed = false;
                            AutoUpdaterDotNET.AutoUpdater.CheckForUpdateEvent += (args) =>
                            {
                                if (runed) return; runed = true;
                                if (args.IsUpdateAvailable)
                                {
                                    Global.HasNewVersion = true;//有新版本
                                    if(Tools.Global.setting.autoUpdate)//开了自动升级功能再开
                                    {
                                        this.Dispatcher.Invoke(new Action(delegate
                                        {
                                            AutoUpdaterDotNET.AutoUpdater.ShowUpdateForm(args);
                                        }));
                                    }
                                }
                            };
                            Random r = new Random();//加上随机参数，确保获取的是最新数据
                            try
                            {
                                AutoUpdaterDotNET.AutoUpdater.Start("https://llcom.papapoi.com/autoUpdate.xml?" + r);
                            }
                            catch
                            {
                                runed = true;
                            }
                        });
                    }

                    //更换标题栏
                    var title = "";
                    title = this.Title;
                    Tools.Global.ChangeTitleEvent += (n, s) =>
                    {
                        this.Dispatcher.Invoke(() => this.Title = title + s);
                    };

                    //热更，防止恶性bug，及时修复
                    new Thread(() =>
                    {
                        try
                        {
                            Random r = new Random();//加上随机参数，确保获取的是最新数据
                            var client = new RestClient("https://llcom.papapoi.com/hotfix.lua?" + r.Next());
                            var request = new RestRequest();
                            var response = client.Get(request);
                            var lua = new LuaEnv.LuaEnv();
                            lua.DoString(response.Content);
                        }
                        catch { }
                    }).Start();

                    Tools.Global.RefreshLuaScriptListEvent += (n, s) =>
                    {
                        this.Dispatcher.Invoke(() => RefreshScriptList());
                    };
                }));
            });
            recvScriptBackup = Tools.Global.setting.recvScript;
            if (string.IsNullOrEmpty(recvScriptBackup)) recvScriptBackup = "default";
        }

        private bool DoInvoke(Action action)
        {
            if (Tools.Global.isMainWindowsClosed)
                return false;
            Dispatcher.Invoke(action);
            return true;
        }

        /// <summary>
        /// 加载快捷发送区数据
        /// </summary>
        private void LoadQuickSendList()
        {
            if (Tools.Global.setting.quickSend.Count == 0)
            {
                Tools.Global.setting.quickSend = new List<ToSendData>
                        {
                            new ToSendData{id = 1,text="example string",commit="右击更改此处文字",hex=false},
                            new ToSendData{id = 2,text="lua可通过接口获取此处数据",hex=false},
                            new ToSendData{id = 3,text="aa 01 02 0d 0a",commit="Hex数据也能发",hex=true},
                            new ToSendData{id = 4,text="此处数据会被lua处理",hex=false},
                            new ToSendData{id = 5,text="右击序号可以更改这一行的位置",hex=false},
                            new ToSendData{id = 6,text="",hex=false},
                        };
            }
            foreach (var i in Tools.Global.setting.quickSend)
            {
                if (i.commit == null)
                    i.commit = TryFindResource("QuickSendButton") as string ?? "?!";
                toSendListItems.Add(i);
            }
            CheckToSendListId();
            QuickListSwitchMenuItem.Header = Global.setting.GetQuickListNameNow() + " ▾";
        }

        private void Uart_UartDataSent(object sender, EventArgs e)
        {
            Tools.Logger.ShowData(sender as byte[], true);
        }

        private void Uart_UartDataRecived(object sender, EventArgs e)
        {
            Tools.Logger.ShowData(sender as byte[], false);
        }

        private void RefreshScriptList()
        {
            //刷新文件列表
            DirectoryInfo luaFileDir = new DirectoryInfo(Tools.Global.ProfilePath + "user_script_run/");
            FileSystemInfo[] luaFiles = luaFileDir.GetFileSystemInfos();
            fileLoading = true;
            luaFileList.Items.Clear();
            for (int i = 0; i < luaFiles.Length; i++)
            {
                FileInfo file = luaFiles[i] as FileInfo;
                //是文件
                if (file != null && file.Name.ToLower().EndsWith(".lua"))
                {
                    string name = file.Name.Substring(0, file.Name.Length - 4);
                    luaFileList.Items.Add(name);
                    if (name== Tools.Global.setting.runScript)
                    {
                        luaFileList.SelectedIndex = luaFileList.Items.Count - 1;
                    }
                }
            }
            lastLuaFile = Tools.Global.setting.runScript;
            fileLoading = false;
        }

        private static int UsbPluginDeley = 0;
        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == 0x219 && !Tools.Global.uart.IsOpen())// 监听USB设备插拔消息
            {
                if (UsbPluginDeley == 0)
                {
                    ++UsbPluginDeley;   // Task启动需要准备时间,这里提前对公共变量加一
                    Task.Run(() =>
                    {
                        do Task.Delay(100).Wait();
                        while (++UsbPluginDeley < 10);
                        UsbPluginDeley = 0;
                        Dispatcher.Invoke(() =>
                        {
                            UsbDeviceNotifier_OnDeviceNotify();
                        });
                        Logger.AddUartLogInfo($"[USB拔插事件] {DateTime.Now:HH:mm:ss.fff}");
                    });
                }
                else UsbPluginDeley = 1;
                handled = true;
            }
            return IntPtr.Zero;
        }
        private void UsbDeviceNotifier_OnDeviceNotify()
        {
            var serialPage = SerialPortFrame.Content as Pages.SerialPortPage;
            if (serialPage != null)
                serialPage.RefreshPortList();
        }

        /// <summary>
        /// 供各数据接口页更新状态栏显示
        /// </summary>
        public void SetDataInterfaceStatus(string text)
        {
            if (statusTextBlock != null)
                statusTextBlock.Text = text ?? "";
        }

        /// <summary>
        /// 根据所有数据接口的打开状态刷新状态栏，显示所有已打开的接口
        /// </summary>
        public void RefreshDataInterfaceStatus()
        {
            if (statusTextBlock == null || DataInterfaceComboBox == null)
                return;
            var parts = new List<string>();
            foreach (var content in new[] {
                SerialPortFrame?.Content,
                tcpClientFrame?.Content,
                udpLocalTestFrame?.Content,
                tcpLocalTestFrame?.Content })
            {
                var text = (content as IDataInterfaceStatusProvider)?.GetStatusBarText();
                if (!string.IsNullOrEmpty(text))
                    parts.Add(text);
            }
            statusTextBlock.Text = string.Join(" | ", parts);
        }

        private void DataInterfaceComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded)
                return;
            RefreshDataInterfaceStatus();
        }

        private void statusTextBlock_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (DataInterfaceComboBox.SelectedIndex == 0 && Tools.Global.uart.IsOpen())
            {
                Tools.Global.setting.SentCount = 0;
                Tools.Global.setting.ReceivedCount = 0;
                RefreshDataInterfaceStatus();
            }
        }

        /// <summary>
        /// 响应其他代码传来的窗口置顶事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void topEvent(object sender, EventArgs e)
        {
            this.Topmost = (bool)sender;
        }

        /// <summary>
        /// Pin 按钮点击：切换窗口置顶
        /// </summary>
        private void PinTopmostButton_Click(object sender, RoutedEventArgs e)
        {
            if (PinTopmostButton?.IsChecked == true)
                Tools.Global.setting.topmost = true;
            else
                Tools.Global.setting.topmost = false;
        }

        /// <summary>
        /// 最大化/还原按钮点击
        /// </summary>
        private void MaximizeRestoreButton_Click(object sender, RoutedEventArgs e)
        {
            if (WindowState == WindowState.Maximized)
                SystemCommands.RestoreWindow(this);
            else
                SystemCommands.MaximizeWindow(this);
        }

        /// <summary>
        /// 窗口状态变化时更新最大化/还原按钮图标和 ToolTip
        /// </summary>
        private void MainWindow_StateChanged(object sender, EventArgs e)
        {
            if (MaximizeRestoreIcon == null || MaximizeRestoreButton == null) return;
            if (WindowState == WindowState.Maximized)
            {
                MaximizeRestoreIcon.Icon = FontAwesomeIcon.Compress;
                MaximizeRestoreButton.ToolTip = TryFindResource("WindowRestore") as string ?? "还原";
            }
            else
            {
                MaximizeRestoreIcon.Icon = FontAwesomeIcon.Expand;
                MaximizeRestoreButton.ToolTip = TryFindResource("WindowMaximize") as string ?? "最大化";
            }
        }

        /// <summary>
        /// 窗口关闭事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Tools.Global.setting.windowLeft = this.Left;
            Tools.Global.setting.windowTop = this.Top;
            Tools.Global.setting.windowWidth = this.Width;
            Tools.Global.setting.windowHeight = this.Height;
            //自动保存脚本
            if (lastLuaFile != "")
                saveLuaFile(lastLuaFile);
            //保存发送/接收脚本
            foreach (var ctrl in FindVisualChildren<View.Controls.SendScriptControl>(this))
                ctrl.SaveOnUnload();
            foreach (var ctrl in FindVisualChildren<View.Controls.RecvScriptControl>(this))
                ctrl.SaveOnUnload();
            Tools.Global.isMainWindowsClosed = true;
            foreach (Window win in App.Current.Windows)
            {
                if (win != this)
                {
                    win.Close();
                }
            }
            e.Cancel = false;//正常关闭
        }



        private void SystemSettingsPanel_Loaded(object sender, RoutedEventArgs e)
        {
            settingsContentPanel.DataContext = Tools.Global.setting;
            languageComboBox.SelectedIndex = Tools.Global.setting.language == "en-US" ? 1 : 0;
            modeComboBox.SelectedIndex = Tools.Global.setting.darkMode ? 1 : 0;
        }

        private void LanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (languageComboBox.SelectedItem is ComboBoxItem item && item.Tag != null)
            {
                Tools.Global.setting.language = item.Tag.ToString();
            }
        }

        private void ModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (modeComboBox.SelectedItem is ComboBoxItem item && item.Tag != null)
            {
                Tools.Global.setting.darkMode = item.Tag.ToString() == "1";
            }
        }

        private void ApiDocumentButton_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Process.Start(Tools.Global.apiDocumentUrl);
        }

        private void OpenScriptFolderButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Diagnostics.Process.Start("explorer.exe", Tools.Global.GetTrueProfilePath() + "user_script_run");
            }
            catch
            {
                Tools.MessageBox.Show($"尝试打开文件夹失败，请自行打开该路径：{Tools.Global.GetTrueProfilePath()}user_script_run");
            }
        }

        private void RefreshScriptListButton_Click(object sender, RoutedEventArgs e)
        {
            RefreshScriptList();
        }

        private void SendUartData_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            if (DataInterfaceComboBox.SelectedIndex != 0)
                return;
            var serialPage = SerialPortFrame.Content as Pages.SerialPortPage;
            serialPage?.PerformSend();
        }

        private void AddSendListButton_Click(object sender, RoutedEventArgs e)
        {
            toSendListItems.Add(new ToSendData() { id = toSendListItems.Count + 1, text = "", hex = false , commit = TryFindResource("QuickSendButton") as string ?? "?!" });
        }

        private void DeleteSendListButton_Click(object sender, RoutedEventArgs e)
        {
            if (toSendListItems.Count > 0)
            {
                toSendListItems.RemoveAt(toSendListItems.Count - 1);
            }
            SaveSendList(null, EventArgs.Empty);
        }

        private void knowSendDataButton_click(object sender, RoutedEventArgs e)
        {
            ToSendData data = ((Button)sender).Tag as ToSendData;

            // 如果有指定接收脚本，则切换
            if (!string.IsNullOrEmpty(data.recvScriptPath))
            {
                //检查文件是否存在
                if (!File.Exists(Tools.Global.ProfilePath + $"user_script_recv_convert/{data.recvScriptPath}.lua"))
                {
                    Tools.Global.setting.recvScript = "default";
                    data.recvScriptPath = "";
                    if (!File.Exists(Tools.Global.ProfilePath + $"user_script_recv_convert/{Tools.Global.setting.recvScript}.lua"))
                    {
                        File.Create(Tools.Global.ProfilePath + $"user_script_recv_convert/{Tools.Global.setting.recvScript}.lua").Close();
                    }
                }
                else
                {
                    Tools.Global.setting.recvScript = data.recvScriptPath;
                }
            }
            else
            {
                Tools.Global.setting.recvScript = recvScriptBackup;
            }

            var sendData = data.hex ? Global.Hex2Byte(data.text) : Global.GetEncoding().GetBytes(data.text);
            Global.recvPara = new byte[][] { Global.GetEncoding().GetBytes(data.recvScriptPara), sendData };
            var serialPage = SerialPortFrame.Content as Pages.SerialPortPage;
            serialPage?.SendUartData(sendData, true);
        }

        private void Button_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // 恢复原有的双击改名功能
            ToSendData data = ((Button)sender).Tag as ToSendData;
            Tuple<bool, string> ret = Tools.InputDialog.OpenDialog(
                TryFindResource("QuickSendSetButton") as string ?? "?!",
                data.commit, 
                TryFindResource("QuickSendChangeButton") as string ?? "?!");
            if(ret.Item1)
            {
                ((Button)sender).Content = data.commit = ret.Item2;
            }
        }

        /// <summary>
        /// 检查并更正快捷发送区序号
        /// </summary>
        public void CheckToSendListId()
        {
            //当序号不对时，更正序号
            for (int i = 0; i < toSendListItems.Count; i++)
            {
                if (toSendListItems[i].id != i + 1)
                {
                    var item = toSendListItems[i];
                    toSendListItems.RemoveAt(i);//元素删掉重新加进去
                    item.id = i + 1;
                    toSendListItems.Insert(i, item);
                }
            }
        }

        public void SaveSendList(object sender, EventArgs e)
        {
            if (!canSaveSendList)
                return;
            CheckToSendListId();
            //保存当前的所有数据
            var newList = new List<ToSendData>();
            foreach (ToSendData i in toSendListItems)
            {
                newList.Add(i);
            }
            Tools.Global.setting.quickSend = newList;
        }

        private void NewScriptButton_Click(object sender, RoutedEventArgs e)
        {
            newLuaFileWrapPanel.Visibility = Visibility.Visible;
        }

        private void RunScriptButton_Click(object sender, RoutedEventArgs e)
        {
            if (luaFileList.SelectedItem != null && !fileLoading)
            {
                luaLogTextBox.Clear();
                LuaEnv.LuaRunEnv.New($"user_script_run/{luaFileList.SelectedItem as string}.lua");
                luaScriptEditorGrid.Visibility = Visibility.Collapsed;
                luaLogShowGrid.Visibility = Visibility.Visible;
                luaLogPrintable = true;
            }
            LuaEnv.LuaRunEnv.canRun = true;
        }

        private void NewLuaFilebutton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(newLuaFileNameTextBox.Text))
            {
                Tools.MessageBox.Show(TryFindResource("LuaNoName") as string ?? "?!");
                return;
            }
            if (File.Exists(Tools.Global.ProfilePath + $"user_script_run/{newLuaFileNameTextBox.Text}.lua"))
            {
                Tools.MessageBox.Show(TryFindResource("LuaExist") as string ?? "?!");
                return;
            }

            try
            {
                File.Create(Tools.Global.ProfilePath + $"user_script_run/{newLuaFileNameTextBox.Text}.lua").Close();
                loadLuaFile(newLuaFileNameTextBox.Text);
            }
            catch
            {
                Tools.MessageBox.Show(TryFindResource("LuaCreateFail") as string ?? "?!");
                return;
            }
            newLuaFileWrapPanel.Visibility = Visibility.Collapsed;
        }

        private void NewLuaFileCancelbutton_Click(object sender, RoutedEventArgs e)
        {
            newLuaFileWrapPanel.Visibility = Visibility.Collapsed;
        }

        //重载锁，防止逻辑卡死
        private static bool fileLoading = false;
        //上次打开文件名
        private static string lastLuaFile = "";
        //最后打开文件的时间
        private static DateTime lastLuaFileTime = DateTime.Now;
        //最后修改文件的时间
        private static DateTime lastLuaChangeTime = DateTime.Now;
        /// <summary>
        /// 加载lua脚本文件
        /// </summary>
        /// <param name="fileName">文件名，不带.lua</param>
        private void loadLuaFile(string fileName)
        {
            //检查文件是否存在
            if (!File.Exists(Tools.Global.ProfilePath + $"user_script_run/{fileName}.lua"))
            {
                Tools.Global.setting.runScript = "example";
                if (!File.Exists(Tools.Global.ProfilePath + $"user_script_run/{Tools.Global.setting.runScript}.lua"))
                {
                    File.Create(Tools.Global.ProfilePath + $"user_script_run/{Tools.Global.setting.runScript}.lua").Close();
                }
            }
            else
            {
                Tools.Global.setting.runScript = fileName;
            }

            //文件内容显示出来
            try
            {
                textEditor.Text = File.ReadAllText(Tools.Global.ProfilePath + $"user_script_run/{Tools.Global.setting.runScript}.lua");
            }
            catch
            {
                Tools.MessageBox.Show("File load failed.\r\n" +
                    "Do not open this file in other application!");
                return;
            }
            
            //记录最后时间
            lastLuaFileTime = File.GetLastWriteTime(Tools.Global.ProfilePath + $"user_script_run/{Tools.Global.setting.runScript}.lua");
            //加载文件,修改时间使用文件时间
            lastLuaChangeTime = lastLuaFileTime;

            RefreshScriptList();
        }

        /// <summary>
        /// 保存lua文件
        /// </summary>
        /// <param name="fileName">文件名，不带.lua</param>
        private void saveLuaFile(string fileName)
        {
            try
            {
                //如果修改时间大于文件时间才执行保存操作
                if (lastLuaChangeTime > lastLuaFileTime)
                {
                    File.WriteAllText(Tools.Global.ProfilePath + $"user_script_run/{fileName}.lua", textEditor.Text);
                    //记录最后时间
                    lastLuaFileTime = File.GetLastWriteTime(Tools.Global.ProfilePath + $"user_script_run/{fileName}.lua");
                }
            }
            catch { }
        }

        private void LuaFileList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (luaFileList.SelectedItem != null && !fileLoading)
            {
                if (lastLuaFile != "")
                    saveLuaFile(lastLuaFile);
                string fileName = luaFileList.SelectedItem as string;
                loadLuaFile(fileName);
            }
        }
        private void ApplyScriptEditorTheme(bool darkMode)
        {
            var asm = System.Reflection.Assembly.GetExecutingAssembly();
            var name = asm.GetName().Name + (darkMode ? ".Lua-dark.xshd" : ".Lua.xshd");
            using (var s = asm.GetManifestResourceStream(name))
            {
                if (s != null)
                {
                    using (var reader = new XmlTextReader(s))
                    {
                        var xshd = HighlightingLoader.LoadXshd(reader);
                        textEditor.SyntaxHighlighting = HighlightingLoader.Load(xshd, HighlightingManager.Instance);
                    }
                }
            }
        }

        private void TextEditor_LostFocus(object sender, RoutedEventArgs e)
        {
            //自动保存脚本
            if (lastLuaFile != "")
                saveLuaFile(lastLuaFile);
        }
        private void Window_Deactivated(object sender, EventArgs e)
        {
            //窗口变为后台,可能在切换编辑器,自动保存脚本
            if (lastLuaFile != "")
                saveLuaFile(lastLuaFile);
        }
        private void Window_Activated(object sender, EventArgs e)
        {
            if (lastLuaFile != "")
            {
                //当前文件最后时间
                DateTime fileTime = File.GetLastWriteTime(Tools.Global.ProfilePath + $"user_script_run/{lastLuaFile}.lua");
                if (fileTime > lastLuaFileTime)//代码在外部被修改
                {
                    loadLuaFile(lastLuaFile);
                }
            }
        }

        //是否可打印标记
        private bool _luaLogPrintable = true;
        private bool luaLogPrintable
        {
            get
            {
                return _luaLogPrintable;
            }
            set
            {
                this.Dispatcher.Invoke(new Action(delegate
                {
                    if (value)
                    {
                        pauseLuaPrintButton.ToolTip = TryFindResource("LuaPause") as string ?? "?!";
                        pauseLuaPrintIcon.Icon = FontAwesomeIcon.Pause;
                    }
                    else
                    {
                        pauseLuaPrintButton.ToolTip = TryFindResource("LuaContinue") as string ?? "?!";
                        pauseLuaPrintIcon.Icon = FontAwesomeIcon.Play;
                    }
                }));
                _luaLogPrintable = value;
            }
        }

        //lua日志打印次数
        private int luaLogCount = 0;
        /// <summary>
        /// 消息来的信号量
        /// </summary>
        private EventWaitHandle luaWaitQueue = new AutoResetEvent(false);
        private List<string> luaLogsBuff = new List<string>();
        private void LuaApis_PrintLuaLog(object sender, EventArgs e)
        {
            if(sender is string && sender != null)
            { 
                lock(luaLogsBuff)
                {
                    if (luaLogsBuff.Count > 500)
                    {
                        luaLogsBuff.Clear();
                        luaLogsBuff.Add("too many logs!");
                        //延时0.5秒，防止卡住ui线程
                        Thread.Sleep(500);
                    }
                    else
                        luaLogsBuff.Add(sender as string);
                }
                luaWaitQueue.Set();
            }
        }

        private void LuaLogPrintTask()
        {
            luaWaitQueue.Reset();
            Tools.Global.ProgramClosedEvent += (_, _) =>
            {
                luaWaitQueue.Set();
            };
            while (true)
            {
                luaWaitQueue.WaitOne();
                if (Tools.Global.isMainWindowsClosed)
                    return;
                var logsb = new StringBuilder();
                lock (luaLogsBuff)
                {
                    for(int i=0;i<luaLogsBuff.Count;i++)
                    {
                        logsb.AppendLine(luaLogsBuff[i]);
                        luaLogCount++;
                    }
                    luaLogsBuff.Clear();
                }

                if (!luaLogPrintable)
                    continue;
                if (logsb.Length == 0)
                    continue;
                var logs = logsb.ToString();
                DoInvoke(()=>
                {
                    luaLogTextBox.IsEnabled = false;//确保文字不再被选中，防止wpf卡死
                    if (luaLogCount >= 1000)
                    {
                        luaLogTextBox.Clear();
                        luaLogTextBox.AppendText("Lua log too long, auto clear.\r\n" +
                            "more logs see lua log file.\r\n");
                        luaLogCount = 0;
                    }
                    luaLogTextBox.AppendText(logs);
                    luaLogTextBox.ScrollToEnd();
                    if (!luaLogTextBox.IsMouseOver)
                        luaLogTextBox.IsEnabled = true;
                });
                //正常就延时10ms，防止卡住ui线程
                Thread.Sleep(10);
            }
        }


        private void luaLogTextBox_MouseLeave(object sender, MouseEventArgs e)
        {
            luaLogTextBox.IsEnabled = true;
        }

        private void StopLuaButton_Click(object sender, RoutedEventArgs e)
        {
            luaLogCount = 0;
            lock(luaLogsBuff)
                luaLogsBuff.Clear();
            if (!LuaEnv.LuaRunEnv.isRunning)
            {
                luaLogTextBox.Clear();
                luaScriptEditorGrid.Visibility = Visibility.Visible;
                luaLogShowGrid.Visibility = Visibility.Collapsed;
                luaLogPrintable = true;
                
                stopLuaOrExitIcon.Icon = FontAwesomeIcon.Stop;
                stopLuaButton.ToolTip = TryFindResource("LuaStop") as string ?? "?!";
            }
            else
            {
                stopLuaOrExitIcon.Icon = FontAwesomeIcon.SignOut;
                stopLuaButton.ToolTip = TryFindResource("LuaQuit") as string ?? "?!";
            }
            luaLogPrintable = true;
            LuaEnv.LuaRunEnv.StopLua("");

            pauseLuaPrintButton.ToolTip = TryFindResource("LuaOverload") as string ?? "?!";
            pauseLuaPrintIcon.Icon = FontAwesomeIcon.Refresh;
        }

        private void LuaRunEnv_LuaRunError(object sender, EventArgs e)
        {
            luaLogPrintable = true;
        }

        private void PauseLuaPrintButton_Click(object sender, RoutedEventArgs e)
        {
            if (!LuaEnv.LuaRunEnv.isRunning)
            {
                stopLuaOrExitIcon.Icon = FontAwesomeIcon.Stop;
                stopLuaButton.ToolTip = TryFindResource("LuaStop") as string ?? "?!";
                LuaEnv.LuaRunEnv.New($"user_script_run/{luaFileList.SelectedItem as string}.lua");
                LuaEnv.LuaRunEnv.canRun = true;
                luaLogPrintable = true;
            }
            else {
                luaLogPrintable = !luaLogPrintable;
            }
        }

        private void SendLuaScriptButton_Click(object sender, RoutedEventArgs e)
        {
            LuaEnv.LuaRunEnv.RunCommand(runOneLineLuaTextBox.Text);
            //runOneLineLuaTextBox.Clear();
        }

        private void RunOneLineLuaTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if(e.Key == Key.Enter)
                LuaEnv.LuaRunEnv.RunCommand(runOneLineLuaTextBox.Text);
        }

        private void ScriptShareButton_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Process.Start("https://github.com/chenxuuu/llcom/blob/master/scripts");
        }

        private void ImportSSCOMButton_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.OpenFileDialog OpenFileDialog = new System.Windows.Forms.OpenFileDialog();
            OpenFileDialog.Filter = TryFindResource("QuickSendSSCOMFile") as string ?? "?!";
            if (OpenFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                this.Dispatcher.Invoke(new Action(delegate
                {
                    canSaveSendList = false;
                    foreach (var i in Tools.Global.ImportFromSSCOM(OpenFileDialog.FileName))
                    {
                        toSendListItems.Add(new ToSendData()
                        {
                            id = toSendListItems.Count + 1,
                            text = i.text,
                            hex = i.hex,
                            commit = i.commit
                        });
                    }
                    canSaveSendList = true;
                    SaveSendList(0, EventArgs.Empty);//保存并刷新数据列表
                }));
            }
        }


        //id序号右击事件
        private void TextBlock_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            ToSendData data;
            try
            {
                data = ((TextBlock)sender).Tag as ToSendData;
            }
            catch
            {
                data = ((Grid)sender).Tag as ToSendData;
            }
            Tuple<bool, string> ret = Tools.InputDialog.OpenDialog(TryFindResource("QuickSendChangeIdButton") as string ?? "?!",
                data.id.ToString(), (TryFindResource("QuickSendChangeIdTitle") as string ?? "?!") + data.id.ToString());

            if (!ret.Item1)
                return;
            CheckToSendListId();
            if (ret.Item2.Trim().Length == 0)//留空删除该项目
            {
                toSendListItems.RemoveAt(data.id-1);
            }
            else
            {
                int index = -1;
                int.TryParse(ret.Item2, out index);
                if (index == data.id || index <= 0 || index > toSendListItems.Count) return;
                //移动到指定位置
                var item = toSendListItems[data.id-1];
                toSendListItems.RemoveAt(data.id-1);
                toSendListItems.Insert(index - 1, item);
            }
            SaveSendList(null, EventArgs.Empty);
        }

        private void MenuItem_Click_QuickSendList(object sender, RoutedEventArgs e)
        {
            canSaveSendList = false;
            int select = int.Parse((string)((MenuItem)sender).Tag);
            toSendListItems.Clear();
            Global.setting.quickSendSelect = select;
            LoadQuickSendList();
            QuickListSwitchMenuItem.Header = Global.setting.GetQuickListNameNow() + " ▾";
            canSaveSendList = true;
        }

        private void QuickSendImportButton_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.OpenFileDialog OpenFileDialog = new System.Windows.Forms.OpenFileDialog();
            OpenFileDialog.Filter = TryFindResource("QuickSendLLCOMFile") as string ?? "?!";
            if (OpenFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                List<ToSendData> data = null;
                try
                {
                    data = JsonConvert.DeserializeObject<List<ToSendData>>(
                        File.ReadAllText(OpenFileDialog.FileName));
                }
                catch (Exception err)
                {
                    Tools.MessageBox.Show(err.Message);
                    return;
                }
                this.Dispatcher.Invoke(new Action(delegate
                {
                    canSaveSendList = false;
                    foreach(var d in data)
                    {
                        toSendListItems.Add(d);
                    }
                    canSaveSendList = true;
                    SaveSendList(0, EventArgs.Empty);//保存并刷新数据列表
                }));
            }
        }

        private void QuickSendExportButton_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.SaveFileDialog SaveFileDialog = new System.Windows.Forms.SaveFileDialog();
            SaveFileDialog.FileName = System.Text.RegularExpressions.Regex.Replace(Global.setting.GetQuickListNameNow(), "[<>/\\|:\"?*]", "-");
            SaveFileDialog.Filter = TryFindResource("QuickSendLLCOMFile") as string ?? "?!";
            if (SaveFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                try
                {
                    File.WriteAllText(SaveFileDialog.FileName, JsonConvert.SerializeObject(toSendListItems));
                    Tools.MessageBox.Show(TryFindResource("QuickSendSaveFileDone") as string ?? "?!");
                }
                catch(Exception err)
                {
                    Tools.MessageBox.Show(err.Message);
                }
            }
        }

        private void QuickListRename_Click(object sender, RoutedEventArgs e)
        {
            Tuple<bool, string> ret = Tools.InputDialog.OpenDialog("↓↓↓↓↓↓",
                Global.setting.GetQuickListNameNow(), TryFindResource("QuickSendListNameChangeTip") as string ?? "?!");

            if (!ret.Item1)
                return;

            Global.setting.SetQuickListNameNow(ret.Item2);
            QuickListSwitchMenuItem.Header = ret.Item2 + " ▾";
        }

        private void pauseLuaPrintButton_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            luaLogTextBox.Clear();
        }

        private void textEditor_TextChanged(object sender, EventArgs e)
        {
            lastLuaChangeTime = DateTime.Now;
        }

        private void removeAllButton_Click(object sender, RoutedEventArgs e)
        {
            var dialogResult = Tools.InputDialog.OpenDialog(TryFindResource("DeleteConfirmationMsg") as string ?? "?!",
                "", TryFindResource("DeleteConfirmation") as string ?? "?!");
            if (dialogResult.Item1 && dialogResult.Item2 == "YES")
            {
                toSendListItems.Clear();
                SaveSendList(null, EventArgs.Empty);
            }
        }

        private void uartDataFlowDocument_GotFocus(object sender, RoutedEventArgs e)
        {
            if (Tools.Global.setting.terminal)
                dataShowFrame.BorderBrush = new SolidColorBrush(Color.FromRgb(0, 148, 0));
        }

        private void uartDataFlowDocument_LostFocus(object sender, RoutedEventArgs e)
        {
            dataShowFrame.BorderBrush = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));
        }

        private void uartDataFlowDocument_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if (e.TextComposition.Text.Length < 1 || !Tools.Global.setting.terminal)
                return;
            if (Tools.Global.uart.IsOpen())
                try
                {
                    Tools.Global.uart.SendData(Encoding.ASCII.GetBytes(e.TextComposition.Text));
                }
                catch { }
        }

        private void uartDataFlowDocument_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (!(Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl)) ||
                !Tools.Global.setting.terminal)
                return;
            if (e.Key >= Key.A && e.Key <= Key.Z && Tools.Global.uart.IsOpen())
                try
                {
                    Tools.Global.uart.SendData(new byte[] { (byte)((int)e.Key - (int)Key.A + 1) });
                }
                catch { }
        }

        private void ScriptIcon_Click(object sender, MouseButtonEventArgs e)
        {
            // 点击📜图标时配置接收脚本
            TextBlock icon = sender as TextBlock;
            ToSendData data = icon.Tag as ToSendData;
            recvScriptCombo.ItemsSource = Directory.GetFiles(Global.ProfilePath + "user_script_recv_convert", "*.lua")
                                                   .Select(System.IO.Path.GetFileNameWithoutExtension).ToList();
            recvScriptPopup.PlacementTarget = icon;
            recvScriptCombo.Tag = data;
            recvScriptCombo.SelectedItem = data.recvScriptPath ?? "";
            recvScriptCombo.IsDropDownOpen = true;
            recvScriptPopup.IsOpen = false;
            recvScriptPopup.IsOpen = true;

            // 打开对话框，选择接收脚本
            //System.Windows.Forms.OpenFileDialog dialog = new System.Windows.Forms.OpenFileDialog();
            //dialog.Filter = "Lua脚本文件 (*.lua)|*.lua|所有文件 (*.*)|*.*";
            //dialog.InitialDirectory = System.IO.Path.Combine(Tools.Global.ProfilePath, "user_script_recv_convert");

            //if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            //{
            //    data.recvScriptPath = System.IO.Path.GetFileNameWithoutExtension(dialog.FileName);
            //    //SaveSendList(null, EventArgs.Empty);
            //}
        }

        private void ScriptIcon_RightClick(object sender, MouseButtonEventArgs e)
        {
            // 右击📜图标时清除接收脚本
            TextBlock icon = sender as TextBlock;
            ToSendData data = icon.Tag as ToSendData;

            // 清除接收脚本项
            if (!string.IsNullOrEmpty(data.recvScriptPath))
            {
                data.recvScriptPath = "";
                //SaveSendList(null, EventArgs.Empty);
            }
        }

        private void recvScriptCombo_DropDownClosed(object sender, EventArgs e)
        {
            ComboBox me = sender as ComboBox;
            ToSendData data = me.Tag as ToSendData;
            string newItem = me.SelectedItem as string;
            if(data.recvScriptPath != newItem) data.recvScriptPath = newItem;
            recvScriptPopup.IsOpen = false;
            me.SelectedItem = null;
        }

        [DllImport("user32")]
        public static extern IntPtr SetFocus(IntPtr hWnd);
        private async void ScriptParaIcon_Click(object sender, MouseButtonEventArgs e)
        {
            TextBlock icon = sender as TextBlock;
            ToSendData data = icon.Tag as ToSendData;

            recvScriptParaBox.Tag = data;
            recvScriptParaBox.Text = data.recvScriptPara;
            recvScriptParaBox.ScrollToEnd();
            recvScriptParaPopup.PlacementTarget = icon;
            recvScriptParaPopup.IsOpen = false;
            await Task.Yield();
            recvScriptParaPopup.IsOpen = true;
            await Task.Yield();
            var source = (HwndSource)PresentationSource.FromVisual(recvScriptParaPopup.Child);
            SetFocus(source.Handle);
            await Task.Yield();
            Keyboard.Focus(recvScriptParaBox);
        }
        private void ScriptParaIcon_RightClick(object sender, MouseButtonEventArgs e)
        {
            TextBlock icon = sender as TextBlock;
            ToSendData data = icon.Tag as ToSendData;

            if (!string.IsNullOrEmpty(data.recvScriptPara))
            {
                data.recvScriptPara = "";
                //SaveSendList(null, EventArgs.Empty);
            }
        }
        private void ScriptParaConfirm_Click(object sender, MouseButtonEventArgs e)
        {
            TextBlock icon = sender as TextBlock;
            TextEditor t = icon.Tag as TextEditor;
            ToSendData data = t.Tag as ToSendData;

            data.recvScriptPara = t.Text;
            //SaveSendList(null, EventArgs.Empty);
            recvScriptParaPopup.IsOpen = false;
        }
        private void ScriptParaCancel_Click(object sender, MouseButtonEventArgs e)
        {
            recvScriptParaPopup.IsOpen = false;
        }

        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
        {
            if (depObj == null) yield break;
            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(depObj); i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(depObj, i);
                if (child is T t)
                    yield return t;
                foreach (var descendant in FindVisualChildren<T>(child))
                    yield return descendant;
            }
        }
    }
}
