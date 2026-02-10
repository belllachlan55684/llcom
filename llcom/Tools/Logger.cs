using Serilog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;

namespace llcom.Tools
{
    enum LogFileEntryType { UartInfo, UartDebug, Lua }

    struct LogFileEntry
    {
        public LogFileEntryType Type;
        public string Message;
    }

    class Logger
    {
        //清空显示的回调函数
        public static event EventHandler DataClearEvent;

        private static readonly BlockingCollection<DataShow> _pendingShowQueue = new BlockingCollection<DataShow>(new ConcurrentQueue<DataShow>());

        /// <summary>
        /// 从待显示队列取出一条，有数据立即返回，无数据则阻塞最多 timeoutMs 毫秒。供 DataShowPage 后台消费线程逐条消费。
        /// </summary>
        public static bool TryTakeOne(out DataShow item, int timeoutMs)
        {
            return _pendingShowQueue.TryTake(out item, timeoutMs);
        }

        /// <summary>
        /// 清空待显示队列
        /// </summary>
        public static void ClearPendingQueue()
        {
            while (_pendingShowQueue.TryTake(out _)) { }
        }

        //清空日志显示
        public static void ClearData()
        {
            ClearPendingQueue();
            DataClearEvent?.Invoke(null, null);
        }
        //显示日志数据（入队，由 DataShowPage 后台消费线程通过 BlockingCollection.TryTakeOne(50ms) 逐条消费）
        public static void ShowData(byte[] data, bool send, string interfaceKey = null, bool isRawSend = false)
        {
            if (Tools.Global.setting.DisableLog && !Tools.Global.setting.enableAnsiColor)
                return;
            _pendingShowQueue.Add(new DataShowPara
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
            if (Tools.Global.setting.DisableLog && !Tools.Global.setting.enableAnsiColor)
                return;
            _pendingShowQueue.Add(s);
        }


        private static Serilog.Core.Logger uartLogFile = null;
        private static Serilog.Core.Logger luaLogFile = null;
        private static readonly ConcurrentQueue<LogFileEntry> _fileLogQueue = new ConcurrentQueue<LogFileEntry>();
        private static volatile bool _fileLogWorkerRun = true;
        private static Thread _fileLogWorkerThread = null;
        private static readonly object _fileLogWorkerLock = new object();
        private static readonly ManualResetEvent _fileLogWorkerIdle = new ManualResetEvent(false);

        private static void EnsureFileLogWorkerStarted()
        {
            lock (_fileLogWorkerLock)
            {
                if (_fileLogWorkerThread != null && _fileLogWorkerThread.IsAlive) return;
                _fileLogWorkerRun = true;
                _fileLogWorkerThread = new Thread(FileLogWorkerEntry) { IsBackground = true };
                _fileLogWorkerThread.Start();
            }
        }

        private static void FileLogWorkerEntry()
        {
            while (_fileLogWorkerRun || !_fileLogQueue.IsEmpty)
            {
                if (_fileLogQueue.TryDequeue(out var entry))
                {
                    try
                    {
                        if (entry.Type == LogFileEntryType.UartInfo || entry.Type == LogFileEntryType.UartDebug)
                        {
                            if (uartLogFile == null) InitUartLogInternal();
                            if (uartLogFile != null)
                            {
                                if (entry.Type == LogFileEntryType.UartInfo)
                                    uartLogFile.Information(entry.Message);
                                else
                                    uartLogFile.Debug(entry.Message);
                            }
                        }
                        else
                        {
                            if (luaLogFile == null) InitLuaLogInternal();
                            if (luaLogFile != null)
                                luaLogFile.Information(entry.Message);
                        }
                    }
                    catch { }
                }
                else
                {
                    _fileLogWorkerIdle.Set();
                    if (!_fileLogWorkerRun) break;
                    Thread.Sleep(5);
                }
            }
        }

        private static void InitUartLogInternal()
        {
            try
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
                uartLogFile.Information("[START]Logs by LLCOM. https://github.com/chenxuuu/llcom");
            }
            catch { }
        }

        private static void InitLuaLogInternal()
        {
            try
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
            catch { }
        }

        /// <summary>
        /// 初始化串口日志文件（按实例隔离，避免多开冲突）；首次写日志时由工作线程自动初始化
        /// </summary>
        public static void InitUartLog()
        {
            EnsureFileLogWorkerStarted();
        }

        public static void CloseUartLog()
        {
            var log = uartLogFile;
            uartLogFile = null;
            log?.Dispose();
        }

        /// <summary>
        /// 写入一条串口日志（仅当 autoSaveLog 为 true 时入队，由后台线程写入文件）
        /// </summary>
        public static void AddUartLogInfo(string l)
        {
            if (Tools.Global.setting?.autoSaveLog != true)
                return;
            EnsureFileLogWorkerStarted();
            EnqueueFileLog(LogFileEntryType.UartInfo, l);
        }

        /// <summary>
        /// 写入一条串口调试日志（仅当 autoSaveLog 为 true 时入队，由后台线程写入文件）
        /// </summary>
        public static void AddUartLogDebug(string l)
        {
            if (Tools.Global.setting?.autoSaveLog != true)
                return;
            EnsureFileLogWorkerStarted();
            EnqueueFileLog(LogFileEntryType.UartDebug, l);
        }

        private static void EnqueueFileLog(LogFileEntryType type, string message)
        {
            if (string.IsNullOrEmpty(message)) return;
            _fileLogWorkerIdle.Reset();
            _fileLogQueue.Enqueue(new LogFileEntry { Type = type, Message = message });
        }

        /// <summary>
        /// 初始化lua日志文件（按实例隔离，避免多开冲突）
        /// </summary>
        public static void InitLuaLog()
        {
            EnsureFileLogWorkerStarted();
        }

        public static void CloseLuaLog()
        {
            var log = luaLogFile;
            luaLogFile = null;
            log?.Dispose();
        }

        /// <summary>
        /// 写入一条lua日志（仅当 autoSaveLog 为 true 时入队，由后台线程写入文件）
        /// </summary>
        public static void AddLuaLog(string l)
        {
            if (Tools.Global.setting?.autoSaveLog != true)
                return;
            EnsureFileLogWorkerStarted();
            EnqueueFileLog(LogFileEntryType.Lua, l);
        }

        /// <summary>
        /// 关闭文件日志管线，等待队列消费完毕（最多 3 秒）
        /// </summary>
        public static void ShutdownFileLog(bool waitForDrain = true)
        {
            _fileLogWorkerRun = false;
            if (waitForDrain)
            {
                for (int i = 0; i < 60 && !_fileLogQueue.IsEmpty; i++)
                    _fileLogWorkerIdle.WaitOne(50);
                _fileLogWorkerThread?.Join(1000);
            }
            var u = uartLogFile;
            var l = luaLogFile;
            uartLogFile = null;
            luaLogFile = null;
            u?.Dispose();
            l?.Dispose();
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
}
