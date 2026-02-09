using LibUsbDotNet.Info;
using LibUsbDotNet.LibUsb;
using llcom.Model;
using Microsoft.Win32;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RestSharp;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Management;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Shapes;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace llcom.Tools
{
    class Global
    {
        public static event EventHandler ProgramClosedEvent;
        //api接口文档网址
        public static string apiDocumentUrl = "https://github.com/chenxuuu/llcom/blob/master/LuaApi.md";
        //主窗口是否被关闭？
        private static bool _isMainWindowsClosed = false;
        public static bool isMainWindowsClosed
        {
            get
            {
                return _isMainWindowsClosed;
            }
            set
            {
                _isMainWindowsClosed = value;
                if (value)
                {
                    uart.WaitUartReceive.Set();
                    Logger.ShutdownFileLog(waitForDrain: true);
                    ProgramClosedEvent?.Invoke(null,EventArgs.Empty);
                }
            }
        }
        //给全局使用的设置参数项
        public static Model.Settings setting;
        public static Model.Uart uart = new Model.Uart();

        /// <summary>
        /// 当前实例 ID（用于同文件夹多开时的资源隔离）
        /// </summary>
        public static int InstanceId { get; } = Process.GetCurrentProcess().Id;

        //软件文件名
        private static string _fileName = "";
        public static string FileName
        {
            get
            {
                if (String.IsNullOrWhiteSpace(_fileName))
                {
                    using (var processModule = Process.GetCurrentProcess().MainModule)
                    {
                        _fileName = System.IO.Path.GetFileName(processModule?.FileName);
                    }
                }
                return _fileName;
            }
        }

        //软件根目录
        private static string _appPath = null;
        /// <summary>
        /// 软件根目录（末尾带\）
        /// </summary>
        public static string AppPath
        {
            get
            {
                if (_appPath == null)
                {
                    using (var processModule = Process.GetCurrentProcess().MainModule)
                    {
                        _appPath = System.IO.Path.GetDirectoryName(processModule?.FileName);
                    }
                    if (!_appPath.EndsWith("\\"))
                        _appPath = _appPath + "\\";
                }
                return _appPath;
            }
        }

        //配置文件路径（普通exe时，会被替换为AppPath）
        public static string ProfilePath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + @"\llcom\";

        /// <summary>
        /// 获取实际的ProfilePath路径（目前没啥用了）
        /// </summary>
        /// <returns></returns>
        public static string GetTrueProfilePath()
        {
            return ProfilePath;
        }

        /// <summary>
        /// 在脚本写 Mutex 下执行操作，避免多实例同时写脚本文件冲突。超时返回 false。
        /// </summary>
        public static bool TryRunWithScriptMutex(Action action, int timeoutMs = 3000)
        {
            var mutexName = "Global\\llcom_script_" + Math.Abs(ProfilePath.GetHashCode()).ToString("X");
            try
            {
                using var mutex = new Mutex(false, mutexName);
                if (!mutex.WaitOne(timeoutMs))
                {
                    Tools.MessageBox.Show("其他实例可能正在编辑脚本，请稍后重试");
                    return false;
                }
                try
                {
                    action();
                    return true;
                }
                finally
                {
                    mutex.ReleaseMutex();
                }
            }
            catch (AbandonedMutexException) { action(); return true; }
        }

        /// <summary>
        /// 在 core_script 创建 Mutex 下执行操作，避免多实例首次启动时竞争创建。
        /// </summary>
        public static void RunWithCoreScriptMutex(Action action, int timeoutMs = 10000)
        {
            var mutexName = "Global\\llcom_core_script_" + Math.Abs(ProfilePath.GetHashCode()).ToString("X");
            try
            {
                using var mutex = new Mutex(false, mutexName);
                if (!mutex.WaitOne(timeoutMs))
                    return;
                try
                {
                    action();
                }
                finally
                {
                    mutex.ReleaseMutex();
                }
            }
            catch (AbandonedMutexException) { action(); }
        }

        /// <summary>
        /// 返回默认脚本名称（发送/接收脚本的「原样输出」项），统一为 RawData
        /// </summary>
        public static string GetDefaultScriptName()
        {
            return "RawData";
        }

        /// <summary>
        /// 确保默认脚本 RawData.lua 存在
        /// </summary>
        public static void EnsureDefaultScriptFiles()
        {
            var sendPath = ProfilePath + "user_script_send_convert/RawData.lua";
            var recvPath = ProfilePath + "user_script_recv_convert/RawData.lua";
            if (Directory.Exists(ProfilePath + "user_script_send_convert") && !File.Exists(sendPath))
                CreateFile("DefaultFiles/user_script_send_convert/RawData.lua", sendPath);
            if (Directory.Exists(ProfilePath + "user_script_recv_convert") && !File.Exists(recvPath))
                CreateFile("DefaultFiles/user_script_recv_convert/RawData.lua", recvPath);
        }

        /// <summary>
        /// 是否为应用商店版本？
        /// </summary>
        /// <returns></returns>
        public static bool IsMSIX()
        {
            return AppPath.ToUpper().Contains(@"\PROGRAM FILES\WINDOWSAPPS\");
        }

        /// <summary>
        /// 是否上报bug？低版本.net框架的上报行为将被限制
        /// </summary>
        public static bool ReportBug { get; set; } = true;

        /// <summary>
        /// 是否有新版本？
        /// </summary>
        public static bool HasNewVersion { get; set; } = false;
        public static byte[][] recvPara { get; set; } = null; // recvPara = [ uartPara, uartSendRaw ]

        /// <summary>
        /// 快捷发送区临时覆盖的接收脚本（接口键 -> 脚本名），不持久化
        /// </summary>
        public static Dictionary<string, string> recvScriptTempOverride { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// 获取指定接口的有效接收脚本（优先临时覆盖，否则使用持久配置）
        /// </summary>
        public static string GetEffectiveRecvScriptForInterface(string key)
        {
            if (!string.IsNullOrEmpty(key) && recvScriptTempOverride.TryGetValue(key, out var v) && !string.IsNullOrEmpty(v))
                return v;
            return setting?.GetRecvScriptForInterface(key) ?? "RawData";
        }

        /// <summary>
        /// 获取指定接口的有效接收脚本列表（按顺序执行）。临时覆盖时返回单元素列表。
        /// </summary>
        public static System.Collections.Generic.List<string> GetEffectiveRecvScriptListForInterface(string key)
        {
            if (!string.IsNullOrEmpty(key) && recvScriptTempOverride.TryGetValue(key, out var v) && !string.IsNullOrEmpty(v))
                return new System.Collections.Generic.List<string> { v };
            return setting?.GetRecvScriptListForInterface(key) ?? new System.Collections.Generic.List<string> { "RawData" };
        }

        /// <summary>
        /// 更换软件标题栏文字
        /// </summary>
        public static event EventHandler<string> ChangeTitleEvent;
        public static void ChangeTitle(string s) => ChangeTitleEvent?.Invoke(null, s);

        /// <summary>
        /// 刷新lua脚本列表
        /// </summary>
        public static event EventHandler RefreshLuaScriptListEvent;
        public static void RefreshLuaScriptList() => RefreshLuaScriptListEvent?.Invoke(null, null);

        /// <summary>
        /// 加载配置文件
        /// </summary>
        public static void LoadSetting()
        {
            if (IsMSIX())
            {
                if (Directory.Exists(ProfilePath))
                {
                    //已经开过一次了，那就继续用之前的路径
                }
                else
                {
                    //appdata路径不可靠，用文档路径替代
                    ProfilePath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "\\llcom\\";
                    if (!Directory.Exists(ProfilePath))
                        Directory.CreateDirectory(ProfilePath);
                }
            }
            else
            {
                ProfilePath = AppPath;//普通exe时，直接用软件路径
            }
            //配置文件
            if (File.Exists(ProfilePath + "settings.json"))
            {
                try
                {
                    //cost 309ms
                    var jsonText = File.ReadAllText(ProfilePath + "settings.json");
                    setting = JsonConvert.DeserializeObject<Model.Settings>(jsonText);
                    setting.SentCount = 0;
                    setting.ReceivedCount = 0;
                    setting.DisableLog = false;
                    // 迁移：timeout/packTimeoutValue/bitDelay -> packSize/packByTimeout
                    try
                    {
                        var j = Newtonsoft.Json.Linq.JObject.Parse(jsonText);
                        if (j["timeout"] != null || j["packTimeoutValue"] != null || j["bitDelay"] != null)
                        {
                            var oldTimeout = j["timeout"]?.Value<int?>() ?? j["packTimeoutValue"]?.Value<int?>();
                            if (oldTimeout.HasValue && oldTimeout.Value > 0)
                                setting.packSize = oldTimeout.Value;
                            else if (oldTimeout.HasValue && oldTimeout.Value < 0)
                                setting.packSize = 50; // 原不分包模式
                            if (j["bitDelay"] != null)
                                setting.packByTimeout = j["bitDelay"].Value<bool>();
                        }
                        // 迁移：showTimestamp (bool) -> showTimestampFormat (int)
                        if (j["showTimestamp"] != null && j["showTimestampFormat"] == null)
                            setting.showTimestampFormat = j["showTimestamp"].Value<bool>() ? 1 : 0;
                    }
                    catch { }
                    // 迁移：default 或 原始数据 或 rawdata -> RawData；加上换行回车 -> CRLF
                    if (setting.sendScript == "default" || setting.sendScript == "原始数据" || setting.sendScript == "rawdata")
                        setting.sendScript = "RawData";
                    if (setting.sendScript == "加上换行回车")
                        setting.sendScript = "CRLF";
                    if (setting.sendScript == "解析换行回车的转义字符")
                        setting.sendScript = "ParseEscapeSeq";
                    if (setting.sendScript == "16进制数据")
                        setting.sendScript = "Hex";
                    if (setting.recvScript == "default" || setting.recvScript == "原始数据" || setting.recvScript == "rawdata")
                        setting.recvScript = "RawData";
                }
                catch
                {
                    Tools.MessageBox.Show($"配置文件加载失败！\r\n" +
                        $"如果是配置文件损坏，可前往{ProfilePath}settings.json.bakup查找备份文件\r\n" +
                        $"并使用该文件替换{ProfilePath}settings.json文件恢复配置");
                    Environment.Exit(1);
                }
            }
            else
            {
                if (Directory.GetFiles(ProfilePath).Length > 10)
                {
                    var r = Tools.InputDialog.OpenDialog("检测到当前文件夹有其他文件\r\n" +
                        "建议新建一个文件夹给llcom，并将llcom.exe放入其中\r\n" +
                        "不然当前文件夹会显得很乱哦~\r\n" +
                        "是否想要继续运行呢？", null, "温馨提示");
                    if (!r.Item1)
                        Environment.Exit(1);
                }
                setting = new Model.Settings();
            }
            LoadLanguageFile(setting.language);
            LoadTheme(setting.darkMode);
        }

        /// <summary>
        /// 主题切换事件（供脚本编辑区等更新语法高亮）
        /// </summary>
        public static event EventHandler<bool> ThemeChanged;

        /// <summary>
        /// 检测 Windows 系统是否为暗黑模式（Registry AppsUseLightTheme：1=浅色，0=暗黑）
        /// </summary>
        public static bool IsSystemDarkMode()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    var value = key?.GetValue("AppsUseLightTheme");
                    return value is int i ? i <= 0 : true;
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 更换主题（浅色/暗黑）
        /// </summary>
        public static void LoadTheme(bool darkMode)
        {
            var schemeName = darkMode ? "Dark" : "Light";
            System.Windows.Application.Current.Resources.MergedDictionaries[1] = new System.Windows.ResourceDictionary
            {
                Source = new Uri($"pack://application:,,,/AdonisUI;component/ColorSchemes/{schemeName}.xaml", UriKind.RelativeOrAbsolute)
            };
            ThemeChanged?.Invoke(null, darkMode);
        }

        /// <summary>
        /// 软件打开后，所有东西的初始化流程
        /// </summary>
        public static void Initial()
        {
            //检查.net版本
            var currentVersion = Walterlv.NdpInfo.GetCurrentVersionName();
            try
            {
                if (currentVersion.StartsWith("4."))
                {
                    var sv = int.Parse(currentVersion.Substring(2, 1));
                    if (sv < 6)
                        throw new Exception();
                }
                else
                {
                    throw new Exception();
                }
            }
            catch
            {
                Tools.MessageBox.Show($"本软件仅支持.net framework 4.6.2以上版本，该计算机上的最高版本为{currentVersion}\r\n" +
                    $"你可以选择继续使用，但若运行途中遇到bug，将不会上报给开发者。\r\n" +
                    $"建议升级到最新.net framework版本");
                ReportBug = false;
            }
            //文件名不能改！
            if (FileName.ToUpper() != "LLCOM.EXE")
            {
                Tools.MessageBox.Show("啊呀呀，软件文件名被改了。。。\r\n" +
                    "为了保证软件功能的正常运行，请将exe名改回llcom.exe");
                Environment.Exit(1);
            }
            //C:\Users\chenx\AppData\Local\Temp\7zO05433053\user_script_run
            if (AppPath.ToUpper().Contains(@"\APPDATA\LOCAL\TEMP\") ||
                AppPath.ToUpper().Contains(@"\WINDOWS\TEMP\"))
            {
                Tools.MessageBox.Show("请勿在压缩包内直接打开本软件。");
                Environment.Exit(1);
            }

            if (IsMSIX())//商店软件的文件路径需要手动新建文件夹
            {
                if (!Directory.Exists(ProfilePath))
                {
                    Directory.CreateDirectory(ProfilePath);
                }
                //升级的时候不会自动升级核心脚本，所以先强制删掉再释放，确保是最新的
                if (Directory.Exists(ProfilePath + "core_script"))
                    Directory.Delete(ProfilePath + "core_script", true);
            }

            try
            {
                RunWithCoreScriptMutex(() =>
                {
                    if (!Directory.Exists(ProfilePath + "core_script"))
                    {
                        Directory.CreateDirectory(ProfilePath + "core_script");
                    }
                    CreateFile("DefaultFiles/core_script/head.lua", ProfilePath + "core_script/head.lua", true);
                    CreateFile("DefaultFiles/core_script/JSON.lua", ProfilePath + "core_script/JSON.lua", false);
                    CreateFile("DefaultFiles/core_script/log.lua", ProfilePath + "core_script/log.lua", false);
                    CreateFile("DefaultFiles/core_script/strings.lua", ProfilePath + "core_script/strings.lua", false);
                    CreateFile("DefaultFiles/core_script/sys.lua", ProfilePath + "core_script/sys.lua", true);
                });

                if (!Directory.Exists(ProfilePath + "logs"))
                    Directory.CreateDirectory(ProfilePath + "logs");
                if (!Directory.Exists(ProfilePath + "user_script_run"))
                {
                    Directory.CreateDirectory(ProfilePath + "user_script_run");
                    CreateFile("DefaultFiles/user_script_run/AT控制TCP连接-快发模式.lua", ProfilePath + "user_script_run/AT控制TCP连接-快发模式.lua");
                    CreateFile("DefaultFiles/user_script_run/AT控制TCP连接-慢发模式.lua", ProfilePath + "user_script_run/AT控制TCP连接-慢发模式.lua");
                    CreateFile("DefaultFiles/user_script_run/example.lua", ProfilePath + "user_script_run/example.lua");
                    CreateFile("DefaultFiles/user_script_run/循环发送快捷发送区数据.lua", ProfilePath + "user_script_run/循环发送快捷发送区数据.lua");
                }
                //通用消息通道的demo
                if (!File.Exists(ProfilePath + "user_script_run/channel-demo.lua"))
                    CreateFile("DefaultFiles/user_script_run/channel-demo.lua", ProfilePath + "user_script_run/channel-demo.lua");
                if (!File.Exists(ProfilePath + "user_script_run/随机发送.lua"))
                    CreateFile("DefaultFiles/user_script_run/随机发送.lua", ProfilePath + "user_script_run/随机发送.lua");
                if (!File.Exists(ProfilePath + "user_script_run/绘制正弦曲线.lua"))
                    CreateFile("DefaultFiles/user_script_run/绘制正弦曲线.lua", ProfilePath + "user_script_run/绘制正弦曲线.lua");

                if (!Directory.Exists(ProfilePath + "user_script_run/requires"))
                    Directory.CreateDirectory(ProfilePath + "user_script_run/requires");
                if (!Directory.Exists(ProfilePath + "user_script_run/logs"))
                    Directory.CreateDirectory(ProfilePath + "user_script_run/logs");

                if (!Directory.Exists(ProfilePath + "user_script_send_convert"))
                {
                    Directory.CreateDirectory(ProfilePath + "user_script_send_convert");
                    CreateFile("DefaultFiles/user_script_send_convert/RawData.lua", ProfilePath + "user_script_send_convert/RawData.lua");
                    CreateFile("DefaultFiles/user_script_send_convert/checksum.lua", ProfilePath + "user_script_send_convert/checksum.lua");
                    CreateFile("DefaultFiles/user_script_send_convert/Hex.lua", ProfilePath + "user_script_send_convert/Hex.lua");
                    CreateFile("DefaultFiles/user_script_send_convert/GPS NMEA.lua", ProfilePath + "user_script_send_convert/GPS NMEA.lua");
                    CreateFile("DefaultFiles/user_script_send_convert/CRLF.lua", ProfilePath + "user_script_send_convert/CRLF.lua");
                    CreateFile("DefaultFiles/user_script_send_convert/ParseEscapeSeq.lua", ProfilePath + "user_script_send_convert/ParseEscapeSeq.lua");
                }
                if (!Directory.Exists(ProfilePath + "user_script_recv_convert"))
                {
                    Directory.CreateDirectory(ProfilePath + "user_script_recv_convert");
                    CreateFile("DefaultFiles/user_script_recv_convert/RawData.lua", ProfilePath + "user_script_recv_convert/RawData.lua");
                }
                EnsureDefaultScriptFiles();
                // 迁移：已有 default.lua 或 原始数据.lua 或 rawdata.lua 时重命名为 RawData.lua
                var sendRawPath = ProfilePath + "user_script_send_convert/RawData.lua";
                var sendDefaultPath = ProfilePath + "user_script_send_convert/default.lua";
                if (File.Exists(sendDefaultPath))
                {
                    if (!File.Exists(sendRawPath))
                        File.Move(sendDefaultPath, sendRawPath);
                    else
                        File.Delete(sendDefaultPath);
                }
                var sendRawDataPath = ProfilePath + "user_script_send_convert/原始数据.lua";
                if (File.Exists(sendRawDataPath))
                {
                    if (!File.Exists(sendRawPath))
                        File.Move(sendRawDataPath, sendRawPath);
                    else
                        File.Delete(sendRawDataPath);
                }
                var sendRawDataLegacyPath = ProfilePath + "user_script_send_convert/rawdata.lua";
                if (File.Exists(sendRawDataLegacyPath))
                {
                    if (!File.Exists(sendRawPath))
                        File.Move(sendRawDataLegacyPath, sendRawPath);
                    else
                    {
                        // Windows 下 rawdata.lua 与 RawData.lua 为同一文件，用 Move 更新大小写，勿删除
                        try { File.Move(sendRawDataLegacyPath, sendRawPath); } catch { }
                    }
                }
                var recvRawPath = ProfilePath + "user_script_recv_convert/RawData.lua";
                var recvDefaultPath = ProfilePath + "user_script_recv_convert/default.lua";
                if (File.Exists(recvDefaultPath))
                {
                    if (!File.Exists(recvRawPath))
                        File.Move(recvDefaultPath, recvRawPath);
                    else
                        File.Delete(recvDefaultPath);
                }
                var recvRawDataPath = ProfilePath + "user_script_recv_convert/原始数据.lua";
                if (File.Exists(recvRawDataPath))
                {
                    if (!File.Exists(recvRawPath))
                        File.Move(recvRawDataPath, recvRawPath);
                    else
                        File.Delete(recvRawDataPath);
                }
                var recvRawDataLegacyPath = ProfilePath + "user_script_recv_convert/rawdata.lua";
                if (File.Exists(recvRawDataLegacyPath))
                {
                    if (!File.Exists(recvRawPath))
                        File.Move(recvRawDataLegacyPath, recvRawPath);
                    else
                    {
                        // Windows 下 rawdata.lua 与 RawData.lua 为同一文件，用 Move 更新大小写，勿删除
                        try { File.Move(recvRawDataLegacyPath, recvRawPath); } catch { }
                    }
                }
                var sendCrlfPath = ProfilePath + "user_script_send_convert/CRLF.lua";
                var sendCrlfOldPath = ProfilePath + "user_script_send_convert/加上换行回车.lua";
                if (File.Exists(sendCrlfOldPath))
                {
                    if (!File.Exists(sendCrlfPath))
                        File.Move(sendCrlfOldPath, sendCrlfPath);
                    else
                        File.Delete(sendCrlfOldPath);
                }
                if (!File.Exists(ProfilePath + "user_script_send_convert/CRLF.lua"))
                    CreateFile("DefaultFiles/user_script_send_convert/CRLF.lua", ProfilePath + "user_script_send_convert/CRLF.lua");
                var sendParseEscapePath = ProfilePath + "user_script_send_convert/ParseEscapeSeq.lua";
                var sendParseEscapeOldPath = ProfilePath + "user_script_send_convert/解析换行回车的转义字符.lua";
                if (File.Exists(sendParseEscapeOldPath))
                {
                    if (!File.Exists(sendParseEscapePath))
                        File.Move(sendParseEscapeOldPath, sendParseEscapePath);
                    else
                        File.Delete(sendParseEscapeOldPath);
                }
                if (!File.Exists(ProfilePath + "user_script_send_convert/ParseEscapeSeq.lua"))
                    CreateFile("DefaultFiles/user_script_send_convert/ParseEscapeSeq.lua", ProfilePath + "user_script_send_convert/ParseEscapeSeq.lua");
                var sendHexPath = ProfilePath + "user_script_send_convert/Hex.lua";
                var sendHexOldPath = ProfilePath + "user_script_send_convert/16进制数据.lua";
                if (File.Exists(sendHexOldPath))
                {
                    if (!File.Exists(sendHexPath))
                        File.Move(sendHexOldPath, sendHexPath);
                    else
                        File.Delete(sendHexOldPath);
                }
                if (!File.Exists(ProfilePath + "user_script_send_convert/Hex.lua"))
                    CreateFile("DefaultFiles/user_script_send_convert/Hex.lua", ProfilePath + "user_script_send_convert/Hex.lua");
                if (!File.Exists(ProfilePath + "user_script_recv_convert/绘制曲线.lua"))
                    CreateFile("DefaultFiles/user_script_recv_convert/绘制曲线.lua", ProfilePath + "user_script_recv_convert/绘制曲线.lua");
                if (!File.Exists(ProfilePath + "user_scrispt_recv_convert/绘制曲线-多条.lua"))
                    CreateFile("DefaultFiles/user_script_recv_convert/绘制曲线-多条.lua", ProfilePath + "user_script_recv_convert/绘制曲线-多条.lua");
                if (!File.Exists(ProfilePath + "user_script_recv_convert/绘制曲线-解析结构体.lua"))
                    CreateFile("DefaultFiles/user_script_recv_convert/绘制曲线-解析结构体.lua", ProfilePath + "user_script_recv_convert/绘制曲线-解析结构体.lua");
                if (!File.Exists(ProfilePath + "user_script_recv_convert/JSON格式化.lua"))
                    CreateFile("DefaultFiles/user_script_recv_convert/JSON格式化.lua", ProfilePath + "user_script_recv_convert/JSON格式化.lua");

                CreateFile("DefaultFiles/LICENSE", ProfilePath + "LICENSE", false);
                CreateFile("DefaultFiles/反馈网址.txt", ProfilePath + "反馈网址.txt", false);

                if (IntPtr.Size == 8)
                    CreateFile("DefaultFiles/libusb-1.0-x64.dll", ProfilePath + "libusb-1.0", false);
                else
                    CreateFile("DefaultFiles/libusb-1.0-x86.dll", ProfilePath + "libusb-1.0", false);
            }
            catch (Exception e)
            {
                Tools.MessageBox.Show("生成文件结构失败，请确保本软件处于有读写权限的目录下再打开。\r\n错误信息：" + e.Message);
                Environment.Exit(1);
            }

            //加载配置文件改成单独拎出来了

            //备份一下文件好了（心理安慰）
            if (File.Exists(ProfilePath + "settings.json"))
            {
                if (File.Exists(ProfilePath + "settings.json.bakup"))
                    File.Delete(ProfilePath + "settings.json.bakup");
                File.Copy(ProfilePath + "settings.json", ProfilePath + "settings.json.bakup");
            }

            uart.serial.BaudRate = setting.baudRate;
            uart.serial.Parity = (Parity)setting.parity;
            uart.serial.DataBits = setting.dataBits;
            uart.serial.StopBits = (StopBits)setting.stopBit;
            uart.UartDataRecived += Uart_UartDataRecived;
            uart.UartDataSent += Uart_UartDataSent;
        }

        /// <summary>
        /// 已发送记录到日志
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private static void Uart_UartDataSent(object sender, Model.Uart.UartDataSentEventArgs e)
        {
            if (e?.Data == null) return;
            Logger.AddUartLogInfo($"<-{Byte2Readable(e.Data)}");
            Logger.AddUartLogDebug($"[HEX]{Byte2Hex(e.Data, " ")}");
        }

        /// <summary>
        /// 收到的数据记录到日志
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private static void Uart_UartDataRecived(object sender, EventArgs e)
        {
            Logger.AddUartLogInfo($"->{Byte2Readable((byte[])sender)}");
            Logger.AddUartLogDebug($"[HEX]{Byte2Hex((byte[])sender, " ")}");
        }

        public static Encoding GetEncoding() => Encoding.GetEncoding(setting.encoding);

        /// <summary>
        /// 字符串转hex值
        /// </summary>
        /// <param name="str">字符串</param>
        /// <param name="space">间隔符号</param>
        /// <returns>结果</returns>
        public static string String2Hex(string str, string space)
        {
            return BitConverter.ToString(GetEncoding().GetBytes(str)).Replace("-", space);
        }


        /// <summary>
        /// hex值转字符串
        /// </summary>
        /// <param name="mHex">hex值</param>
        /// <returns>原始字符串</returns>
        public static string Hex2String(string mHex)
        {
            mHex = Regex.Replace(mHex, "[^0-9A-Fa-f]", "");
            if (mHex.Length % 2 != 0)
                mHex = mHex.Remove(mHex.Length - 1, 1);
            if (mHex.Length <= 0) return "";
            byte[] vBytes = new byte[mHex.Length / 2];
            for (int i = 0; i < mHex.Length; i += 2)
                if (!byte.TryParse(mHex.Substring(i, 2), NumberStyles.HexNumber, null, out vBytes[i / 2]))
                    vBytes[i / 2] = 0;
            return GetEncoding().GetString(vBytes);
        }


        /// <summary>
        /// byte转string
        /// </summary>
        /// <param name="mHex"></param>
        /// <returns></returns>
        public static string Byte2String(byte[] vBytes, int len = -1)
        {
            var br = from e in vBytes
                     where e != 0
                     select e;
            if (len == -1 || len > br.Count())
                len = br.Count();
            return GetEncoding().GetString(br.Take(len).ToArray());
        }

        private static byte[] b_del = Encoding.GetEncoding(65001).GetBytes("␡");

        private static byte[][] symbols =
        {
            new byte[]{226,144,128},new byte[]{226,144,129},new byte[]{226,144,130},new byte[]{226,144,131},new byte[]{226,144,132},
            new byte[]{226,144,133},new byte[]{226,144,134},new byte[]{226,144,135},new byte[]{226,144,136},new byte[]{226,144,137},
            new byte[]{226,144,138},new byte[]{226,144,139},new byte[]{226,144,140},new byte[]{226,144,141},new byte[]{226,144,142},
            new byte[]{226,144,143},new byte[]{226,144,144},new byte[]{226,144,145},new byte[]{226,144,146},new byte[]{226,144,147},
            new byte[]{226,144,148},new byte[]{226,144,149},new byte[]{226,144,150},new byte[]{226,144,151},new byte[]{226,144,152},
            new byte[]{226,144,153},new byte[]{226,144,154},new byte[]{226,144,155},new byte[]{226,144,156},new byte[]{226,144,157},
            new byte[]{226,144,158},new byte[]{226,144,159},
        };
        /// <summary>
        /// byte转string（可读）
        /// </summary>
        /// <param name="vBytes"></param>
        /// <param name="len"></param>
        /// <param name="enableSymbol">若为 null 则使用全局 setting.EnableSymbol</param>
        /// <returns></returns>
        public static string Byte2Readable(byte[] vBytes, int len = -1, bool? enableSymbol = null)
        {
            if (len == -1)
                len = vBytes?.Length ?? 0;
            if (vBytes == null)//fix
                return "";
            var useSymbol = enableSymbol ?? setting.EnableSymbol;
            //没开这个功能/非utf8就别搞了
            if (!useSymbol || setting.encoding != 65001)
                return Byte2String(vBytes, len);
            var tb = new List<byte>();
            for (int i = 0; i < len; i++)
            {
                switch(vBytes[i])
                {
                    case 0x0d:
                        //遇到成对出现
                        if(i < len - 1 && vBytes[i+1] == 0x0a)
                        {
                            tb.AddRange(symbols[0x0d]);
                            tb.AddRange(symbols[0x0a]);
                            tb.Add(0x0d);
                            tb.Add(0x0a);
                            i++;
                        }
                        else
                        {
                            tb.AddRange(symbols[0x0d]);
                            tb.Add(vBytes[i]);
                        }
                        break;
                    case 0x0a:
                    case 0x09://tab字符
                        tb.AddRange(symbols[vBytes[i]]);
                        tb.Add(vBytes[i]);
                        break;
                    default:
                        //普通的字符
                        if(vBytes[i] <= 0x1f)
                            tb.AddRange(symbols[vBytes[i]]);
                        else if (vBytes[i] == 0x7f)//del
                            tb.AddRange(b_del);
                        else
                            tb.Add(vBytes[i]);
                        break;
                }
            }
            return GetEncoding().GetString(tb.ToArray());
        }

        /// <summary>
        /// hex转byte
        /// </summary>
        /// <param name="mHex">hex值</param>
        /// <returns>原始字符串</returns>
        public static byte[] Hex2Byte(string mHex)
        {
            mHex = Regex.Replace(mHex, "[^0-9A-Fa-f]", "");
            if (mHex.Length % 2 != 0)
                mHex = mHex.Remove(mHex.Length - 1, 1);
            if (mHex.Length <= 0) return new byte[0];
            byte[] vBytes = new byte[mHex.Length / 2];
            for (int i = 0; i < mHex.Length; i += 2)
                if (!byte.TryParse(mHex.Substring(i, 2), NumberStyles.HexNumber, null, out vBytes[i / 2]))
                    vBytes[i / 2] = 0;
            return vBytes;
        }


        public static string Byte2Hex(byte[] d, string s = "", int len = -1)
        {
            if (len == -1)
                len = d.Length;
            return BitConverter.ToString(d,0,len).Replace("-", s);
        }


        /// <summary>
        /// 导入SSCOM配置文件数据
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static List<Model.ToSendData> ImportFromSSCOM(string path)
        {
            var lines = File.ReadAllLines(path, Encoding.GetEncoding("GB2312"));
            var r = new List<Model.ToSendData>();
            Regex title = new Regex(@"N1\d\d=\d*,");
            for (int i = 0; i < lines.Length; i++)
            {
                try
                {
                    var temp = new Model.ToSendData();
                    //Console.WriteLine(lines[i]);
                    if (title.IsMatch(lines[i]))//匹配上了
                    {
                        var strs = lines[i].Split(",".ToCharArray()[0]);
                        temp.commit = strs[1].Replace(((char)2).ToString(), ",");
                        if (string.IsNullOrWhiteSpace(temp.commit))
                            temp.commit = "发送";
                        //Console.WriteLine(temp.commit);

                        int dot = lines[i + 1].IndexOf(",");
                        temp.hex = lines[i + 1].Substring(dot - 1, 1) == "H";
                        //Console.WriteLine(strs[0].Substring(strs[0].Length - 1));

                        string text = lines[i + 1].Substring(dot + 1);
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            temp.text = text.Replace(((char)2).ToString(), ",");
                            r.Add(temp);
                        }
                    }
                }
                catch
                {
                    //先不处理
                }
            }
            return r;
        }

        /// <summary>
        /// 读取软件资源文件内容
        /// </summary>
        /// <param name="path">路径</param>
        /// <returns>内容字节数组</returns>
        public static byte[] GetAssetsFileContent(string path)
        {
            Uri uri = new Uri(path, UriKind.Relative);
            var source = System.Windows.Application.GetResourceStream(uri).Stream;
            byte[] f = new byte[source.Length];
            source.Read(f, 0, (int)source.Length);
            return f;
        }

        /// <summary>
        /// 取出文件
        /// </summary>
        /// <param name="insidePath">软件内部的路径</param>
        /// <param name="outPath">需要释放到的路径</param>
        /// <param name="d">是否覆盖</param>
        public static void CreateFile(string insidePath, string outPath, bool d = true)
        {
            if(!File.Exists(outPath) || d)
                File.WriteAllBytes(outPath, GetAssetsFileContent(insidePath));
        }

        /// <summary>
        /// 更换语言文件
        /// </summary>
        /// <param name="languagefileName"></param>
        public static void LoadLanguageFile(string languagefileName)
        {
            try
            {
                System.Windows.Application.Current.Resources.MergedDictionaries[0] = new System.Windows.ResourceDictionary()
                {
                    Source = new Uri($"pack://application:,,,/languages/{languagefileName}.xaml", UriKind.RelativeOrAbsolute)
                };
            }
            catch
            {
                System.Windows.Application.Current.Resources.MergedDictionaries[0] = new System.Windows.ResourceDictionary()
                {
                    Source = new Uri("pack://application:,,,/languages/en-US.xaml", UriKind.RelativeOrAbsolute)
                };
            }

        }

        private static string GitHubToken = null;
        /// <summary>
        /// 获取
        /// </summary>
        /// <param name="callback"></param>
        /// <returns></returns>
        public static List<OnlineScript> GetOnlineScripts(Action<int,int> callback = null)
        {
            if(GitHubToken == null)
            {
                try
                {
                    var client = new RestClient("https://llcom.papapoi.com/token.txt");
                    var request = new RestRequest();
                    request.Timeout = 10000;
                    var response = client.Get(request);
                    GitHubToken = response.Content;
                }
                catch
                {
                    return null;
                }
            }
            //请求函数
            var req = (string after) =>
            {
                var client = new RestClient();
                client.BaseUrl = new Uri("https://api.github.com/graphql");
                var request = new RestRequest(RestSharp.Method.POST);
                request.AddHeader("user-agent", "llcom");
                request.AddHeader("Authorization", $"bearer {GitHubToken}");
                request.AddParameter("application/json", 
                    "{\r\n  \"query\": \"query {repository(owner: \\\"chenxuuu\\\", name: \\\"llcom\\\") {discussions(categoryId:\\\"DIC_kwDOCtNzks4CSz35\\\"," +
                    (after == null ? "" : $"after: \"{after}\"") + "first: 100" +
                    ") {totalCount,pageInfo {startCursor,endCursor,hasNextPage,hasPreviousPage},nodes {body,url}}}}\"\r\n}", ParameterType.RequestBody);
                var response = client.Execute(request);
                var j = JsonConvert.DeserializeObject<JObject>(response.Content);
                var bodys = from i in j["data"]["repository"]["discussions"]["nodes"]
                            select ((string)i["body"],(string)i["url"]);
                return (
                    bodys.ToList(),
                    (int)j["data"]["repository"]["discussions"]["totalCount"],
                    (string)j["data"]["repository"]["discussions"]["pageInfo"]["endCursor"],
                    (bool)j["data"]["repository"]["discussions"]["pageInfo"]["hasNextPage"]
                );
            };

            string lastPage = null;
            var scripts = new List<OnlineScript>();
            var pages = 0;
            while(true)
            {
                var (data, total, endCursor, hasNextPage) = req(lastPage);
                foreach (var (s,u) in data)
                {
                    try
                    {
                        var n = new OnlineScript(s);
                        n.Url = u;
                        scripts.Add(n);
                    }
                    catch { }
                }
                callback?.Invoke(pages, total/100);//回调，上报进度
                if (!hasNextPage)
                    break;
                pages++;
                lastPage = endCursor;
            }
            return scripts;
        }

    }
}
