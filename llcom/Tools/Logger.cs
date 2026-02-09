using Serilog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace llcom.Tools
{
    class Logger
    {
        //显示日志数据的回调函数（P1 队列模式：由 DataShowPage 定时器消费，此处保留供兼容）
        public static event EventHandler<DataShow> DataShowTask;
        //清空显示的回调函数
        public static event EventHandler DataClearEvent;

        private static readonly ConcurrentQueue<DataShow> _pendingShowQueue = new ConcurrentQueue<DataShow>();
        private const int BatchSize = 20;

        /// <summary>
        /// 从待显示队列取出一批（最多 BatchSize 条），返回实际取出的数量
        /// </summary>
        public static int DequeueBatch(List<DataShow> outList, int maxCount = BatchSize)
        {
            if (outList == null) return 0;
            int count = 0;
            while (count < maxCount && _pendingShowQueue.TryDequeue(out var item) && item != null)
            {
                outList.Add(item);
                count++;
            }
            return count;
        }

        /// <summary>
        /// 清空待显示队列
        /// </summary>
        public static void ClearPendingQueue()
        {
            while (_pendingShowQueue.TryDequeue(out _)) { }
        }

        //清空日志显示
        public static void ClearData()
        {
            ClearPendingQueue();
            DataClearEvent?.Invoke(null, null);
        }
        //显示日志数据（入队，由 DataShowPage 定时器批量消费）
        public static void ShowData(byte[] data, bool send, string interfaceKey = null, bool isRawSend = false)
        {
            if (Tools.Global.setting.DisableLog)
                return;
            _pendingShowQueue.Enqueue(new DataShowPara
            {
                data = data != null && data.Length > 0 ? data.ToArray() : data,
                send = send,
                interfaceKey = interfaceKey,
                isRawSend = isRawSend,
                time = DateTime.Now
            });
        }
        //显示日志数据（DataShowRaw 无 interfaceKey，使用全局 DisableLog）
        public static void ShowDataRaw(DataShowRaw s)
        {
            if (Tools.Global.setting.DisableLog)
                return;
            _pendingShowQueue.Enqueue(s);
        }


        private static Serilog.Core.Logger uartLogFile = null;
        private static Serilog.Core.Logger luaLogFile = null;

        /// <summary>
        /// 初始化串口日志文件（按实例隔离，避免多开冲突）
        /// </summary>
        public static void InitUartLog()
        {
            var logPath = Tools.Global.ProfilePath + $"logs/log_{Tools.Global.InstanceId}.txt";
            uartLogFile = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console()
                .WriteTo.File(logPath,
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 30,
                    encoding: Encoding.UTF8,
                    rollOnFileSizeLimit: true)
                .CreateLogger();
            AddUartLogInfo("[START]Logs by LLCOM. https://github.com/chenxuuu/llcom");
        }

        public static void CloseUartLog()
        {
            if (uartLogFile == null)
                return;
            uartLogFile.Dispose();
            uartLogFile = null;
        }

        /// <summary>
        /// 写入一条串口日志（仅当 autoSaveLog 为 true 时写入文件）
        /// </summary>
        /// <param name="l"></param>
        public static void AddUartLogInfo(string l)
        {
            if (Tools.Global.setting?.autoSaveLog != true)
                return;
            if (uartLogFile == null)
                InitUartLog();
            uartLogFile.Information(l);
        }
        /// <summary>
        /// 写入一条串口调试日志（仅当 autoSaveLog 为 true 时写入文件）
        /// </summary>
        /// <param name="l"></param>
        public static void AddUartLogDebug(string l)
        {
            if (Tools.Global.setting?.autoSaveLog != true)
                return;
            if (uartLogFile == null)
                InitUartLog();
            uartLogFile.Debug(l);
        }

        /// <summary>
        /// 初始化lua日志文件（按实例隔离，避免多开冲突）
        /// </summary>
        public static void InitLuaLog()
        {
            var logPath = Tools.Global.ProfilePath + $"user_script_run/logs/log_{Tools.Global.InstanceId}.txt";
            luaLogFile = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console()
                .WriteTo.File(logPath,
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 30,
                    encoding: Encoding.UTF8,
                    rollOnFileSizeLimit: true)
                .CreateLogger();
        }

        public static void CloseLuaLog()
        {
            if (luaLogFile == null)
                return;
            luaLogFile.Dispose();
            luaLogFile = null;
        }

        /// <summary>
        /// 写入一条lua日志（仅当 autoSaveLog 为 true 时写入文件）
        /// </summary>
        /// <param name="l"></param>
        public static void AddLuaLog(string l)
        {
            if (Tools.Global.setting?.autoSaveLog != true)
                return;
            if (luaLogFile == null)
                InitLuaLog();
            luaLogFile.Information(l);
        }
    }

    //整个父类统一下
    class DataShow
    {
        public DateTime time { get; set; } = DateTime.Now;
        public byte[] data;
    }

    /// <summary>
    /// 显示到日志显示页面的类
    /// </summary>
    class DataShowPara : DataShow
    {
        public bool send;
        /// <summary>
        /// 数据来源接口键（Serial/TcpClient/TcpLocal 等），用于接收脚本分接口选择
        /// </summary>
        public string interfaceKey;
        /// <summary>
        /// 是否原始发送数据（脚本转换前）
        /// </summary>
        public bool isRawSend;
    }

    /// <summary>
    /// 更通用的日志数据
    /// </summary>
    class DataShowRaw : DataShow
    {
        public string title;
        public SolidColorBrush color;
    }
    class DataShowSendRaw : DataShow { }
}
