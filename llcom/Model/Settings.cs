using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace llcom.Model
{
    [PropertyChanged.AddINotifyPropertyChangedInterface]
    class Settings
    {
        public event EventHandler MainWindowTop;
        private string _dataToSend = "uart data";
        private int _baudRate = 115200;
        private bool _autoReconnect = true;
        private bool _autoSaveLog = false;
        private int _showHexFormat = 0;
        private bool _showSend = true;
        private bool _showSendRaw = true;
        private int _parity = 0;
        private int _timeout = 50;
        private int _packTimeoutValue = 50;
        private int _dataBits = 8;
        private int _stopBit = 1;
        private string _sendScript = "RawData";
        private Dictionary<string, string> _sendScriptByInterface = null;
        private string _recvScript = "RawData";
        private string _runScript = "example";
        private bool _topmost = false;
        public List<List<ToSendData>> quickSendList = new List<List<ToSendData>>();
        private int _quickSendSelect = -1;
        private bool _bitDelay = true;
        private bool _autoUpdate = true;
        private uint _maxLength = 10240;
        private string _language = System.Threading.Thread.CurrentThread.CurrentCulture.Name;
        private int _encoding = 65001;
        private bool _terminal = true;
        private bool _enableSymbol = true;
        private bool _darkMode = false;

        private string _sendDisplayColor = "#CD5C5C";
        private string _recvDisplayColor = "#32CD32";

        //窗口大小与位置
        private double _windowTop = 0;
        public double windowTop { get { return _windowTop; } set { _windowTop = value; Save(); } }
        private double _windowLeft = 0;
        public double windowLeft { get { return _windowLeft; } set { _windowLeft = value; Save(); } }
        private double _windowWidth = 0;
        public double windowWidth { get { return _windowWidth; } set { _windowWidth = value; Save(); } }
        private double _windowHeight = 0;
        public double windowHeight { get { return _windowHeight; } set { _windowHeight = value; Save(); } }

        public int SentCount { get; set; } = 0;
        public int ReceivedCount { get; set; } = 0;

        /// <summary>
        /// 主界面左右分割比例（左侧列 Star 值，默认 11，与右侧 7 对应 11*:7*）
        /// </summary>
        private double _leftColumnStar = 11;
        public double leftColumnStar { get => _leftColumnStar; set { _leftColumnStar = value; Save(); } }

        /// <summary>
        /// 上次选中的串口名（如 COM3），若不存在则重新选择
        /// </summary>
        private string _serialPortName = "";
        public string serialPortName { get => _serialPortName; set { _serialPortName = value ?? ""; Save(); } }

        /// <summary>
        /// 发送数据显示颜色（#RRGGBB 或 #AARRGGBB）
        /// </summary>
        public string sendDisplayColor
        {
            get => _sendDisplayColor;
            set { _sendDisplayColor = value ?? "#CD5C5C"; Save(); }
        }

        /// <summary>
        /// 接收数据显示颜色（#RRGGBB 或 #AARRGGBB）
        /// </summary>
        public string recvDisplayColor
        {
            get => _recvDisplayColor;
            set { _recvDisplayColor = value ?? "#32CD32"; Save(); }
        }

        /// <summary>
        /// 获取发送显示的 SolidColorBrush
        /// </summary>
        public SolidColorBrush GetSendDisplayBrush()
        {
            try
            {
                var color = (Color)ColorConverter.ConvertFromString(_sendDisplayColor);
                return new SolidColorBrush(color);
            }
            catch { return new SolidColorBrush(Color.FromRgb(0xCD, 0x5C, 0x5C)); }
        }

        /// <summary>
        /// 获取接收显示的 SolidColorBrush
        /// </summary>
        public SolidColorBrush GetRecvDisplayBrush()
        {
            try
            {
                var color = (Color)ColorConverter.ConvertFromString(_recvDisplayColor);
                return new SolidColorBrush(color);
            }
            catch { return new SolidColorBrush(Color.FromRgb(0x32, 0xCD, 0x32)); }
        }

        private Dictionary<string, string> _sendDisplayColorByInterface = null;
        private Dictionary<string, string> _recvDisplayColorByInterface = null;

        /// <summary>
        /// 各接口的发送显示颜色，键为接口名（Serial/TcpClient/UdpLocal/TcpLocal/WinUSB）
        /// </summary>
        public Dictionary<string, string> sendDisplayColorByInterface { get => _sendDisplayColorByInterface ??= new Dictionary<string, string>(); set => _sendDisplayColorByInterface = value ?? new Dictionary<string, string>(); }

        /// <summary>
        /// 各接口的接收显示颜色
        /// </summary>
        public Dictionary<string, string> recvDisplayColorByInterface { get => _recvDisplayColorByInterface ??= new Dictionary<string, string>(); set => _recvDisplayColorByInterface = value ?? new Dictionary<string, string>(); }

        public string GetSendDisplayColorForInterface(string key)
        {
            if (key != null && sendDisplayColorByInterface.TryGetValue(key, out var v) && !string.IsNullOrEmpty(v))
                return v;
            return _sendDisplayColor;
        }

        public string GetRecvDisplayColorForInterface(string key)
        {
            if (key != null && recvDisplayColorByInterface.TryGetValue(key, out var v) && !string.IsNullOrEmpty(v))
                return v;
            return _recvDisplayColor;
        }

        public void SetSendDisplayColorForInterface(string key, string value)
        {
            if (string.IsNullOrEmpty(key)) return;
            sendDisplayColorByInterface[key] = value ?? _sendDisplayColor;
            Save();
        }

        public void SetRecvDisplayColorForInterface(string key, string value)
        {
            if (string.IsNullOrEmpty(key)) return;
            recvDisplayColorByInterface[key] = value ?? _recvDisplayColor;
            Save();
        }

        public SolidColorBrush GetSendDisplayBrushForInterface(string key)
        {
            var hex = GetSendDisplayColorForInterface(key);
            try
            {
                var color = (Color)ColorConverter.ConvertFromString(hex);
                return new SolidColorBrush(color);
            }
            catch { return new SolidColorBrush(Color.FromRgb(0xCD, 0x5C, 0x5C)); }
        }

        public SolidColorBrush GetRecvDisplayBrushForInterface(string key)
        {
            var hex = GetRecvDisplayColorForInterface(key);
            try
            {
                var color = (Color)ColorConverter.ConvertFromString(hex);
                return new SolidColorBrush(color);
            }
            catch { return new SolidColorBrush(Color.FromRgb(0x32, 0xCD, 0x32)); }
        }

        /// <summary>
        /// 保存配置到临时文件（运行期，避免多实例互相覆盖）
        /// </summary>
        private void Save()
        {
            var path = Tools.Global.ProfilePath + $"settings_{Tools.Global.InstanceId}.json";
            File.WriteAllText(path, JsonConvert.SerializeObject(this));
        }

        /// <summary>
        /// 关闭时回写主配置到 settings.json
        /// </summary>
        public void SaveToMainConfig()
        {
            var mainPath = Tools.Global.ProfilePath + "settings.json";
            var backupPath = Tools.Global.ProfilePath + "settings.json.bakup";
            if (File.Exists(mainPath) && File.Exists(backupPath))
                File.Delete(backupPath);
            if (File.Exists(mainPath))
                File.Copy(mainPath, backupPath);
            File.WriteAllText(mainPath, JsonConvert.SerializeObject(this));
        }

        /// <summary>
        /// 串口接收每包最大长度
        /// </summary>
        public uint maxLength
        {
            get
            {
                return _maxLength;
            }
            set
            {
                _maxLength = value;
                Save();
            }
        }

        /// <summary>
        /// 当前选中的快捷发送列表数据
        /// </summary>
        public List<ToSendData> quickSend
        {
            get
            {
                if (_quickSendSelect < 0 || _quickSendSelect > 10)
                    return new List<ToSendData>();
                if (quickSendList.Count <= 10)
                {
                    for (var i = 0; i < 10; i++)
                        quickSendList.Add(new List<ToSendData>());
                }
                return quickSendList[_quickSendSelect];
            }
            set
            {
                if (_quickSendSelect < 0 || _quickSendSelect > 10)
                    return;
                if (quickSendList.Count <= 10)
                {
                    for (var i = 0; i < 10; i++)
                        quickSendList.Add(new List<ToSendData>());
                }
                quickSendList[_quickSendSelect] = value;
                Save();
            }
        }

        /// <summary>
        /// 快捷发送选中的目标接口索引列表（持久化）
        /// </summary>
        public List<int> quickSendTargetIndices { get; set; } = new List<int>();

        /// <summary>
        /// 当前选中的快速发送列表编号
        /// </summary>
        public int quickSendSelect
        {
            get
            {
                return _quickSendSelect;
            }
            set
            {
                _quickSendSelect = value;
                Save();
            }
        }

        /// <summary>
        /// 是否开启自动升级
        /// </summary>
        public bool autoUpdate
        {
            get
            {
                return _autoUpdate;
            }
            set
            {
                _autoUpdate = value;
                Save();
            }
        }

        public bool bitDelay
        {
            get
            {
                return _bitDelay;
            }
            set
            {
                _bitDelay = value;
                Save();
            }
        }

        public string dataToSend
        {
            get
            {
                return _dataToSend;
            }
            set
            {
                _dataToSend = value;
                Save();
            }
        }
        public int baudRate
        {
            get
            {
                return _baudRate;
            }
            set
            {
                try
                {
                    Tools.Global.uart.serial.BaudRate = value;
                    _baudRate = value;
                    Save();
                }
                catch(Exception e)
                {
                    Tools.MessageBox.Show(e.Message);
                }
            }
        }

        public bool autoReconnect
        {
            get
            {
                return _autoReconnect;
            }
            set
            {
                _autoReconnect = value;
                Save();
            }
        }

        /// <summary>
        /// 自动保存日志（串口/Lua 日志写入 logs/log.txt 等）
        /// </summary>
        public bool autoSaveLog
        {
            get => _autoSaveLog;
            set
            {
                _autoSaveLog = value;
                if (!value)
                {
                    Tools.Logger.CloseUartLog();
                    Tools.Logger.CloseLuaLog();
                }
                Save();
            }
        }

        /// <summary>
        /// 串口数据显示格式
        /// 0 都显示
        /// 1 只显示字符串
        /// 2 只显示Hex
        /// </summary>
        public int showHexFormat
        {
            get
            {
                return _showHexFormat;
            }
            set
            {
                _showHexFormat = value;
                Save();
            }
        }

        private bool _rts = false;
        private bool _dtr = true;

        /// <summary>
        /// RTS（持久化），透传至 uart 时同步
        /// </summary>
        public bool Rts
        {
            get => _rts;
            set
            {
                _rts = value;
                try { if (Tools.Global.uart.IsOpen()) Tools.Global.uart.serial.RtsEnable = value; } catch { }
                Save();
            }
        }

        /// <summary>
        /// DTR（持久化），透传至 uart 时同步
        /// </summary>
        public bool Dtr
        {
            get => _dtr;
            set
            {
                _dtr = value;
                try { if (Tools.Global.uart.IsOpen()) Tools.Global.uart.serial.DtrEnable = value; } catch { }
                Save();
            }
        }

        public bool showSend
        {
            get
            {
                return _showSend;
            }
            set
            {
                _showSend = value;
                Save();
            }
        }

        public bool showSendRaw
        {
            get
            {
                return _showSendRaw;
            }
            set
            {
                _showSendRaw = value;
                Save();
            }
        }

        public int parity
        {
            get
            {
                return _parity;
            }
            set
            {
                try
                {
                    _parity = value;
                    Tools.Global.uart.serial.Parity = (Parity)value;
                    Save();
                }
                catch (Exception e)
                {
                    Tools.MessageBox.Show(e.Message);
                }
            }
        }

        public int timeout
        {
            get
            {
                return _timeout;
            }
            set
            {
                _timeout = value;
                if (value >= 0)
                    _packTimeoutValue = value;
                Save();
            }
        }

        /// <summary>
        /// 分包模式下的超时值（毫秒），仅当 packEnabled 为 true 时生效
        /// </summary>
        public int packTimeoutValue
        {
            get => _packTimeoutValue;
            set
            {
                _packTimeoutValue = value > 0 ? value : 50;
                if (_timeout >= 0)
                    timeout = _packTimeoutValue;
                else
                    Save();
            }
        }

        /// <summary>
        /// 分包模式：选中=分包，不选中=不分包
        /// </summary>
        [PropertyChanged.DependsOn(nameof(timeout))]
        public bool packEnabled
        {
            get => _timeout >= 0;
            set
            {
                if (value)
                    timeout = _packTimeoutValue;
                else
                    timeout = -1;
            }
        }

        public int dataBits
        {
            get
            {
                return _dataBits;
            }
            set
            {
                try
                {
                    _dataBits = value;
                    Tools.Global.uart.serial.DataBits = value;
                    Save();
                }
                catch (Exception e)
                {
                    Tools.MessageBox.Show(e.Message);
                }
            }
        }

        public int stopBit
        {
            get
            {
                return _stopBit;
            }
            set
            {
                try
                {
                    _stopBit = value;
                    Tools.Global.uart.serial.StopBits = (StopBits)value;
                    Save();
                }
                catch (Exception e)
                {
                    Tools.MessageBox.Show(e.Message);
                }
            }
        }

        public string sendScript
        {
            get
            {
                return _sendScript;
            }
            set
            {
                _sendScript = value;
                Save();
            }
        }

        /// <summary>
        /// 各数据接口独立使用的发送脚本，键为接口名（Serial/TcpClient/TcpLocal 等）
        /// </summary>
        public Dictionary<string, string> sendScriptByInterface
        {
            get => _sendScriptByInterface ??= new Dictionary<string, string>();
            set => _sendScriptByInterface = value ?? new Dictionary<string, string>();
        }

        private Dictionary<string, List<string>> _sendScriptListByInterface = null;

        /// <summary>
        /// 各数据接口独立使用的发送脚本列表（按顺序执行），键为接口名
        /// </summary>
        public Dictionary<string, List<string>> sendScriptListByInterface
        {
            get => _sendScriptListByInterface ??= new Dictionary<string, List<string>>();
            set => _sendScriptListByInterface = value ?? new Dictionary<string, List<string>>();
        }

        /// <summary>
        /// 获取指定接口的发送脚本，无则回退到全局 sendScript
        /// </summary>
        public string GetSendScriptForInterface(string key)
        {
            if (key != null && sendScriptByInterface.TryGetValue(key, out var v) && !string.IsNullOrEmpty(v))
                return v;
            return sendScript;
        }

        /// <summary>
        /// 设置指定接口的发送脚本
        /// </summary>
        public void SetSendScriptForInterface(string key, string value)
        {
            if (string.IsNullOrEmpty(key)) return;
            sendScriptByInterface[key] = value ?? sendScript;
            Save();
        }

        /// <summary>
        /// 获取指定接口的发送脚本列表（按顺序执行）。若 List 未配置，从 sendScriptByInterface 迁移单值。
        /// </summary>
        public List<string> GetSendScriptListForInterface(string key)
        {
            var list = sendScriptListByInterface;
            if (key != null && list.TryGetValue(key, out var v) && v != null && v.Count > 0)
                return v;
            var single = GetSendScriptForInterface(key);
            return string.IsNullOrEmpty(single) ? new List<string> { sendScript } : new List<string> { single };
        }

        /// <summary>
        /// 设置指定接口的发送脚本列表
        /// </summary>
        public void SetSendScriptListForInterface(string key, List<string> value)
        {
            if (string.IsNullOrEmpty(key)) return;
            sendScriptListByInterface[key] = value ?? new List<string> { sendScript };
            Save();
        }

        public string recvScript
        {
            get
            {
                return _recvScript;
            }
            set
            {
                _recvScript = value;
                Save();
            }
        }

        private Dictionary<string, string> _recvScriptByInterface = null;

        /// <summary>
        /// 各数据接口独立使用的接收脚本，键为接口名（Serial/TcpClient/TcpLocal 等）
        /// </summary>
        public Dictionary<string, string> recvScriptByInterface
        {
            get => _recvScriptByInterface ??= new Dictionary<string, string>();
            set => _recvScriptByInterface = value ?? new Dictionary<string, string>();
        }

        private Dictionary<string, List<string>> _recvScriptListByInterface = null;

        /// <summary>
        /// 各数据接口独立使用的接收脚本列表（按顺序执行），键为接口名
        /// </summary>
        public Dictionary<string, List<string>> recvScriptListByInterface
        {
            get => _recvScriptListByInterface ??= new Dictionary<string, List<string>>();
            set => _recvScriptListByInterface = value ?? new Dictionary<string, List<string>>();
        }

        /// <summary>
        /// 获取指定接口的接收脚本，无则回退到全局 recvScript
        /// </summary>
        public string GetRecvScriptForInterface(string key)
        {
            if (key != null && recvScriptByInterface.TryGetValue(key, out var v) && !string.IsNullOrEmpty(v))
                return v;
            return recvScript;
        }

        /// <summary>
        /// 设置指定接口的接收脚本
        /// </summary>
        public void SetRecvScriptForInterface(string key, string value)
        {
            if (string.IsNullOrEmpty(key)) return;
            recvScriptByInterface[key] = value ?? recvScript;
            Save();
        }

        /// <summary>
        /// 获取指定接口的接收脚本列表（按顺序执行）。若 List 未配置，从 recvScriptByInterface 迁移单值。
        /// </summary>
        public List<string> GetRecvScriptListForInterface(string key)
        {
            var list = recvScriptListByInterface;
            if (key != null && list.TryGetValue(key, out var v) && v != null && v.Count > 0)
                return v;
            var single = GetRecvScriptForInterface(key);
            return string.IsNullOrEmpty(single) ? new List<string> { recvScript } : new List<string> { single };
        }

        /// <summary>
        /// 设置指定接口的接收脚本列表
        /// </summary>
        public void SetRecvScriptListForInterface(string key, List<string> value)
        {
            if (string.IsNullOrEmpty(key)) return;
            recvScriptListByInterface[key] = value ?? new List<string> { recvScript };
            Save();
        }

        private Dictionary<string, bool> _showSendByInterface = null;
        public Dictionary<string, bool> showSendByInterface { get => _showSendByInterface ??= new Dictionary<string, bool>(); set => _showSendByInterface = value ?? new Dictionary<string, bool>(); }
        public bool GetShowSendForInterface(string key) { if (key != null && showSendByInterface.TryGetValue(key, out var v)) return v; return showSend; }
        public void SetShowSendForInterface(string key, bool value) { if (string.IsNullOrEmpty(key)) return; showSendByInterface[key] = value; Save(); }

        private Dictionary<string, bool> _showSendRawByInterface = null;
        public Dictionary<string, bool> showSendRawByInterface { get => _showSendRawByInterface ??= new Dictionary<string, bool>(); set => _showSendRawByInterface = value ?? new Dictionary<string, bool>(); }
        public bool GetShowSendRawForInterface(string key) { if (key != null && showSendRawByInterface.TryGetValue(key, out var v)) return v; return showSendRaw; }
        public void SetShowSendRawForInterface(string key, bool value) { if (string.IsNullOrEmpty(key)) return; showSendRawByInterface[key] = value; Save(); }

        private Dictionary<string, string> _dataToSendByInterface = null;
        public Dictionary<string, string> dataToSendByInterface { get => _dataToSendByInterface ??= new Dictionary<string, string>(); set => _dataToSendByInterface = value ?? new Dictionary<string, string>(); }
        public string GetDataToSendForInterface(string key) { if (key != null && dataToSendByInterface.TryGetValue(key, out var v) && v != null) return v; return dataToSend; }
        public void SetDataToSendForInterface(string key, string value) { if (string.IsNullOrEmpty(key)) return; dataToSendByInterface[key] = value ?? dataToSend; Save(); }

        private Dictionary<string, int> _showHexFormatByInterface = null;
        public Dictionary<string, int> showHexFormatByInterface { get => _showHexFormatByInterface ??= new Dictionary<string, int>(); set => _showHexFormatByInterface = value ?? new Dictionary<string, int>(); }
        public int GetShowHexFormatForInterface(string key) { if (key != null && showHexFormatByInterface.TryGetValue(key, out var v)) return v; return showHexFormat; }
        public void SetShowHexFormatForInterface(string key, int value) { if (string.IsNullOrEmpty(key)) return; showHexFormatByInterface[key] = value; Save(); }

        private Dictionary<string, bool> _enableSymbolByInterface = null;
        public Dictionary<string, bool> enableSymbolByInterface { get => _enableSymbolByInterface ??= new Dictionary<string, bool>(); set => _enableSymbolByInterface = value ?? new Dictionary<string, bool>(); }
        public bool GetEnableSymbolForInterface(string key) { if (key != null && enableSymbolByInterface.TryGetValue(key, out var v)) return v; return EnableSymbol; }
        public void SetEnableSymbolForInterface(string key, bool value) { if (string.IsNullOrEmpty(key)) return; enableSymbolByInterface[key] = value; Save(); }

        private Dictionary<string, bool> _hexModeByInterface = null;
        public Dictionary<string, bool> hexModeByInterface { get => _hexModeByInterface ??= new Dictionary<string, bool>(); set => _hexModeByInterface = value ?? new Dictionary<string, bool>(); }
        public bool GetHexForInterface(string key) { if (key != null && hexModeByInterface.TryGetValue(key, out var v)) return v; return false; }
        public void SetHexForInterface(string key, bool value) { if (string.IsNullOrEmpty(key)) return; hexModeByInterface[key] = value; Save(); }

        private bool _enableAnsiColor = true;
        public bool enableAnsiColor { get => _enableAnsiColor; set { _enableAnsiColor = value; Save(); } }

        private bool _showTimestamp = true;
        /// <summary>
        /// 显示时间戳（持久化）
        /// </summary>
        public bool showTimestamp { get => _showTimestamp; set { _showTimestamp = value; Save(); } }

        public string runScript
        {
            get
            {
                return _runScript;
            }
            set
            {
                _runScript = value;
                Save();
            }
        }

        public bool topmost
        {
            get
            {
                return _topmost;
            }
            set
            {
                _topmost = value;
                try
                {
                    MainWindowTop(value, EventArgs.Empty);
                }
                catch { }
                Save();
            }
        }

        public bool terminal
        {
            get
            {
                return _terminal;
            }
            set
            {
                _terminal = value;
                Save();
            }
        }

        public string language
        {
            get
            {
                return _language;
            }
            set
            {
                _language = value;
                Tools.Global.LoadLanguageFile(value);
                Save();
            }
        }

        public int encoding
        {
            get
            {
                return _encoding;
            }
            set
            {
                try
                {
                    Encoding.GetEncoding(value);
                    _encoding = value;
                    Save();
                }
                catch { }//获取出错说明编码不对
            }
        }

        private bool _disableLog = false;
        /// <summary>
        /// 停止打印（全局）
        /// </summary>
        public bool DisableLog { get => _disableLog; set { _disableLog = value; Save(); } }

        public bool EnableSymbol
        {
            get => _enableSymbol;
            set
            {
                _enableSymbol = value;
                Save();
            }
        }

        /// <summary>
        /// 暗黑模式
        /// </summary>
        public bool darkMode
        {
            get => _darkMode;
            set
            {
                _darkMode = value;
                Tools.Global.LoadTheme(value);
                Save();
            }
        }

        private string _mqttServer = "broker.emqx.io";
        private int _mqttPort = 1883;
        private string _mqttClientID = Guid.NewGuid().ToString();
        private bool _mqttTLS = false;
        private bool _mqttTLSCert = false;
        private string _mqttTLSCertCaPath = "";
        private string _mqttTLSCertClientPath = "";
        private string _mqttTLSCertClientPassword = "";
        private bool _mqttWs = false;
        private string _mqttWsPath = "/mqtt";
        private string _mqttUser = "user";
        private string _mqttPassword = "password";
        private int _mqttKeepAlive = 120;
        private bool _mqttCleanSession = false;
        private string _mqttPublishTopic = "your/publish/topic";
        private string _mqttSubscribeTopic = "your/subcribe/topic";
        public string mqttServer { get { return _mqttServer; } set { _mqttServer = value; Save(); } }
        public int mqttPort { get { return _mqttPort; } set { _mqttPort = value; Save(); } }
        public string mqttClientID { get { return _mqttClientID; } set { _mqttClientID = value; Save(); } }
        public bool mqttTLS { get { return _mqttTLS; } set { _mqttTLS = value; Save(); } }
        public bool mqttTLSCert { get { return _mqttTLSCert; } set { _mqttTLSCert = value; Save(); } }
        public string mqttTLSCertCaPath { get { return _mqttTLSCertCaPath; } set { _mqttTLSCertCaPath = value; Save(); } }
        public string mqttTLSCertClientPath { get { return _mqttTLSCertClientPath; } set { _mqttTLSCertClientPath = value; Save(); } }
        public string mqttTLSCertClientPassword { get { return _mqttTLSCertClientPassword; } set { _mqttTLSCertClientPassword = value; Save(); } }
        public bool mqttWs { get { return _mqttWs; } set { _mqttWs = value; Save(); } }
        public string mqttWsPath { get { return _mqttWsPath; } set { _mqttWsPath = value; Save(); } }
        public string mqttUser { get { return _mqttUser; } set { _mqttUser = value; Save(); } }
        public string mqttPassword { get { return _mqttPassword; } set { _mqttPassword = value; Save(); } }
        public int mqttKeepAlive { get { return _mqttKeepAlive; } set { _mqttKeepAlive = value; Save(); } }
        public bool mqttCleanSession { get { return _mqttCleanSession; } set { _mqttCleanSession = value; Save(); } }
        public string mqttPublishTopic { get { return _mqttPublishTopic; } set { _mqttPublishTopic = value; Save(); } }
        public string mqttSubscribeTopic { get { return _mqttSubscribeTopic; } set { _mqttSubscribeTopic = value; Save(); } }


        private string _quickListName0 = "未命名0";
        public string quickListName0 { get { return _quickListName0; } set { _quickListName0 = value; Save(); } }

        private string _quickListName1 = "未命名1";
        public string quickListName1 { get { return _quickListName1; } set { _quickListName1 = value; Save(); } }

        private string _quickListName2 = "未命名2";
        public string quickListName2 { get { return _quickListName2; } set { _quickListName2 = value; Save(); } }

        private string _quickListName3 = "未命名3";
        public string quickListName3 { get { return _quickListName3; } set { _quickListName3 = value; Save(); } }

        private string _quickListName4 = "未命名4";
        public string quickListName4 { get { return _quickListName4; } set { _quickListName4 = value; Save(); } }

        private string _quickListName5 = "未命名5";
        public string quickListName5 { get { return _quickListName5; } set { _quickListName5 = value; Save(); } }

        private string _quickListName6 = "未命名6";
        public string quickListName6 { get { return _quickListName6; } set { _quickListName6 = value; Save(); } }

        private string _quickListName7 = "未命名7";
        public string quickListName7 { get { return _quickListName7; } set { _quickListName7 = value; Save(); } }

        private string _quickListName8 = "未命名8";
        public string quickListName8 { get { return _quickListName8; } set { _quickListName8 = value; Save(); } }

        private string _quickListName9 = "未命名9";
        public string quickListName9 { get { return _quickListName9; } set { _quickListName9 = value; Save(); } }

        public string GetQuickListNameNow()
        {
            return _quickSendSelect switch
            {
                0 => quickListName0,
                1 => quickListName1,
                2 => quickListName2,
                3 => quickListName3,
                4 => quickListName4,
                5 => quickListName5,
                6 => quickListName6,
                7 => quickListName7,
                8 => quickListName8,
                9 => quickListName9,
                _ => "??",
            };
        }
        public void SetQuickListNameNow(string name)
        {
            switch (_quickSendSelect)
            {
                case 0:
                    quickListName0 = name;
                    break;
                case 1:
                    quickListName1 = name;
                    break;
                case 2:
                    quickListName2 = name;
                    break;
                case 3:
                    quickListName3 = name;
                    break;
                case 4:
                    quickListName4 = name;
                    break;
                case 5:
                    quickListName5 = name;
                    break;
                case 6:
                    quickListName6 = name;
                    break;
                case 7:
                    quickListName7 = name;
                    break;
                case 8:
                    quickListName8 = name;
                    break;
                case 9:
                    quickListName9 = name;
                    break;
                default:
                    break;
            }
        }




        private string _tcpClientServer = "qq.com";
        private int _tcpClientPort = 80;
        private int _tcpClientProtocolType = 0;
        public string tcpClientServer { get { return _tcpClientServer; } set { _tcpClientServer = value; Save(); } }
        public int tcpClientPort { get { return _tcpClientPort; } set { _tcpClientPort = value; Save(); } }
        public int tcpClientProtocolType { get { return _tcpClientProtocolType; } set { _tcpClientProtocolType = value; Save(); } }


        private int _tcpServerPort = 2333;
        public int tcpServerPort { get { return _tcpServerPort; } set { _tcpServerPort = value; Save(); } }

        private bool _tcpReconnect = false;
        public bool tcpReconnect { get { return _tcpReconnect; } set { _tcpReconnect = value; Save(); } }
        private int _tcpReconnectInterval = 5;
        public int tcpReconnectInterval { get { return _tcpReconnectInterval; } set { _tcpReconnectInterval = value; Save(); } }

        private int _udpServerPort = 2333;
        public int udpServerPort { get { return _udpServerPort; } set { _udpServerPort = value; Save(); } }

        private bool _luaTestHex = false;
        private bool _luaTestHexRev = false;
        public bool luaTestHex { get { return _luaTestHex; } set { _luaTestHex = value; Save(); } }
        public bool luaTestHexRev { get { return _luaTestHexRev; } set { _luaTestHexRev = value; Save(); } }
    }
}
