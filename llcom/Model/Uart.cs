using llcom.LuaEnv;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Management;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace llcom.Model
{
    class Uart
    {
        //废弃的串口对象，存放处，尝试fix[System.ObjectDisposedException: 已关闭 Safe handle]
        //https://drdump.com/Problem.aspx?ProblemID=524533
        private List<SerialPort> useless = new List<SerialPort>();

        public SerialPort serial = new SerialPort();
        public event EventHandler UartDataRecived;
        public event EventHandler UartDataSent;
        private Stream lastPortBaseStream = null;
        private bool _rts = false;
        private bool _dtr = true;

        public bool Rts
        {
            get
            {
                return _rts;
            }
            set
            {
                Tools.Global.uart.serial.RtsEnable = _rts = value;
            }
        }
        public bool Dtr
        {
            get
            {
                return _dtr;
            }
            set
            {
                Tools.Global.uart.serial.DtrEnable = _dtr = value;
            }
        }

        private static readonly object objLock = new object();
        
        /// <summary>
        /// 初始化串口各个触发函数
        /// </summary>
        public Uart()
        {
            //声明接收到事件
            serial.DataReceived += Serial_DataReceived;
            serial.RtsEnable = Tools.Global.setting != null ? Tools.Global.setting.Rts : Rts;
            serial.DtrEnable = Tools.Global.setting != null ? Tools.Global.setting.Dtr : Dtr;
            new Thread(ReadData).Start();

            //适配一下通用通道
            LuaApis.SendChannelsRegister("uart", (data, _) => 
            {
                if (IsOpen() && data != null)
                {
                    SendData(data);
                    return true;
                }
                else
                    return false;
            });
        }

        /// <summary>
        /// 刷新串口对象
        /// </summary>
        private void refreshSerialDevice()
        {
#if DEBUG
            var st = new StackTrace(1, true);
            Tools.Logger.AddUartLogDebug($"[refreshSerialDevice]entry caller={st.GetFrame(0)?.GetMethod()?.DeclaringType?.Name}.{st.GetFrame(0)?.GetMethod()?.Name}");
#endif
            Tools.Logger.AddUartLogDebug($"[refreshSerialDevice]start");
            try
            {
                Tools.Logger.AddUartLogDebug($"[refreshSerialDevice]lastPortBaseStream.Dispose");
                Task.Run(() =>//这行代码会卡住，我扔task里还卡吗？
                {
                    try
                    {
                        lastPortBaseStream?.Dispose();
                    }
                    catch { }
                });
            }
            catch (Exception e)
            {
                Tools.Logger.AddUartLogDebug($"[refreshSerialDevice]lastPortBaseStream.Dispose error:{e.Message}");
                Console.WriteLine($"portBaseStream?.Dispose error:{e.Message}");
            }
            try
            {
                Tools.Logger.AddUartLogDebug($"[refreshSerialDevice]BaseStream.Dispose");
                Task.Run(() =>//这行代码会卡住，我扔task里还卡吗？
                {
                    try
                    {
                        serial.BaseStream.Dispose();
                    }
                    catch { }
                });
            }
            catch (Exception e)
            {
                Tools.Logger.AddUartLogDebug($"[refreshSerialDevice]BaseStream.Dispose error:{e.Message}");
                Console.WriteLine($"serial.BaseStream.Dispose error:{e.Message}");
            }
            Tools.Logger.AddUartLogDebug($"[refreshSerialDevice]Dispose");
            Task.Run(() =>//我服了
            {
                try
                {
                    serial.Dispose();
                }
                catch { }
            });
            Tools.Logger.AddUartLogDebug($"[refreshSerialDevice]new");
            lock(useless)//存起来
                useless.Add(serial);
            serial = new SerialPort();
            //声明接收到事件
            serial.DataReceived += Serial_DataReceived;
            serial.BaudRate = Tools.Global.setting.baudRate;
            serial.Parity = (Parity)Tools.Global.setting.parity;
            serial.DataBits = Tools.Global.setting.dataBits;
            serial.StopBits = (StopBits)Tools.Global.setting.stopBit;
            serial.RtsEnable = Tools.Global.setting != null ? Tools.Global.setting.Rts : Rts;
            serial.DtrEnable = Tools.Global.setting != null ? Tools.Global.setting.Dtr : Dtr;
            Tools.Logger.AddUartLogDebug($"[refreshSerialDevice]done");
        }

        /// <summary>
        /// 获取串口设备COM名
        /// </summary>
        /// <returns></returns>
        public string GetName()
        {
            return serial.PortName;
        }

        /// <summary>
        /// 设置串口设备COM名
        /// </summary>
        /// <returns></returns>
        public void SetName(string s)
        {
            serial.PortName = s;
        }

        /// <summary>
        /// 查看串口打开状态
        /// </summary>
        /// <returns></returns>
        public bool IsOpen()
        {
            return serial.IsOpen;
        }

        /// <summary>
        /// 开启串口
        /// </summary>
        public void Open()
        {
#if DEBUG
            var st = new StackTrace(1, true);
            Tools.Logger.AddUartLogDebug($"[UartOpen]entry IsOpen={serial.IsOpen} PortName={serial.PortName} caller={st.GetFrame(0)?.GetMethod()?.DeclaringType?.Name}.{st.GetFrame(0)?.GetMethod()?.Name}");
#endif
            string temp = serial.PortName;
            // 若已打开且端口名有效，跳过 refreshSerialDevice 和 Open，避免不必要的 Dispose 导致连接中断
            if (serial.IsOpen && !string.IsNullOrEmpty(temp))
            {
                Tools.Logger.AddUartLogDebug($"[UartOpen]already open, skip");
                return;
            }
            Tools.Logger.AddUartLogDebug($"[UartOpen]refreshSerialDevice");
            refreshSerialDevice();
            serial.PortName = temp;
            Tools.Logger.AddUartLogDebug($"[UartOpen]open");
            serial.Open();
            lastPortBaseStream = serial.BaseStream;
            Tools.Logger.AddUartLogDebug($"[UartOpen]done");
        }

        /// <summary>
        /// 关闭串口
        /// </summary>
        public void Close()
        {
#if DEBUG
            var st = new StackTrace(1, true);
            Tools.Logger.AddUartLogDebug($"[UartClose]entry IsOpen={serial.IsOpen} caller={st.GetFrame(0)?.GetMethod()?.DeclaringType?.Name}.{st.GetFrame(0)?.GetMethod()?.Name}");
#endif
            Tools.Logger.AddUartLogDebug($"[UartClose]refreshSerialDevice");
            refreshSerialDevice();
            Tools.Logger.AddUartLogDebug($"[UartClose]Close");
            serial.Close();
            Tools.Logger.AddUartLogDebug($"[UartClose]done");
        }

        /// <summary>
        /// 发送数据
        /// </summary>
        /// <param name="data">数据内容</param>
        public void SendData(byte[] data, byte[] dataRaw = null)
        {
            if (data.Length == 0)
                return;
            serial.Write(data, 0, data.Length);
            Tools.Global.setting.SentCount += data.Length;
            bool showRaw = dataRaw != null && Tools.Global.setting.GetShowSendRawForInterface("Serial");
            bool showConverted = Tools.Global.setting.GetShowSendForInterface("Serial");
            if (showRaw && showConverted && dataRaw != null && data.SequenceEqual(dataRaw))
                UartDataSent(data, EventArgs.Empty);
            else
            {
                if (showRaw && dataRaw != null) UartDataSent(dataRaw, EventArgs.Empty);
                if (showConverted) UartDataSent(data, EventArgs.Empty);
            }
        }

        //收到串口事件的信号量
        public EventWaitHandle WaitUartReceive = new AutoResetEvent(true);
        //接收到事件
        private void Serial_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            WaitUartReceive.Set();
        }

        private void EmitPacket(byte[] data)
        {
            if (data == null || data.Length == 0) return;
            Tools.Global.setting.ReceivedCount += data.Length;
            try
            {
                UartDataRecived(data, EventArgs.Empty);
                LuaApis.SendChannelsReceived("uart", data);
            }
            catch { }
        }

        /// <summary>
        /// 单独开个线程接收数据
        /// </summary>
        private void ReadData()
        {
            WaitUartReceive.Reset();
            while (true)
            {
                WaitUartReceive.WaitOne();
                if (Tools.Global.isMainWindowsClosed)
                    return;
                var packSize = Tools.Global.setting.packSize;
                var packByTimeout = Tools.Global.setting.packByTimeout;
                var baudRate = Tools.Global.setting.baudRate;
                var maxLength = Tools.Global.setting.maxLength;
                var timeoutMs = Math.Max(1, packSize * 10 * 1000 / baudRate);

                if (packByTimeout)
                {
                    System.Threading.Thread.Sleep(timeoutMs);
                    List<byte> result = new List<byte>();
                    while (true)
                    {
                        if (serial == null || !serial.IsOpen)
                            break;
                        try
                        {
                            int length = serial.BytesToRead;
                            if (length == 0)
                                break;
                            byte[] rev = new byte[length];
                            serial.Read(rev, 0, length);
                            if (rev.Length == 0)
                                break;
                            result.AddRange(rev);
                        }
                        catch { break; }

                        if (result.Count > maxLength)
                            break;
                        System.Threading.Thread.Sleep(timeoutMs);
                    }
                    if (result.Count > 0)
                        EmitPacket(result.ToArray());
                }
                else
                {
                    List<byte> result = new List<byte>();
                    while (true)
                    {
                        if (serial == null || !serial.IsOpen)
                            break;
                        try
                        {
                            int length = serial.BytesToRead;
                            if (length == 0)
                            {
                                if (result.Count > 0)
                                    EmitPacket(result.ToArray());
                                break;
                            }
                            byte[] rev = new byte[length];
                            serial.Read(rev, 0, length);
                            if (rev.Length == 0)
                                break;
                            result.AddRange(rev);
                        }
                        catch { break; }

                        while (result.Count >= packSize)
                        {
                            var toEmit = result.GetRange(0, packSize).ToArray();
                            result.RemoveRange(0, packSize);
                            EmitPacket(toEmit);
                        }
                        if (result.Count > maxLength)
                        {
                            EmitPacket(result.ToArray());
                            result.Clear();
                            break;
                        }
                    }
                }
            }
        }
    }
}
