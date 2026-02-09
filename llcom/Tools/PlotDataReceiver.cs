using System;
using System.Threading;
using llcom.LuaEnv;
using llcom.Model;

namespace llcom.Tools
{
    /// <summary>
    /// 常驻数据接收层：订阅 LinePlotAdd，维护 10 条线的环形缓冲。
    /// 无论曲线窗口是否打开，始终接收并缓存数据。
    /// </summary>
    public class PlotDataReceiver
    {
        public const int MaxLines = 10;
        public const int MaxPointsPerLine = 500;

        private readonly double[][] _buffers = new double[MaxLines][];
        private readonly int[] _counts = new int[MaxLines];
        private readonly int[] _heads = new int[MaxLines];
        private readonly object _lock = new object();

        private static PlotDataReceiver _instance;
        private static readonly object _initLock = new object();

        public static PlotDataReceiver Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_initLock)
                    {
                        if (_instance == null)
                            _instance = new PlotDataReceiver();
                    }
                }
                return _instance;
            }
        }

        private PlotDataReceiver()
        {
            for (int i = 0; i < MaxLines; i++)
            {
                _buffers[i] = new double[MaxPointsPerLine];
            }
            LuaApis.LinePlotAdd += OnLinePlotAdd;
        }

        private void OnLinePlotAdd(object sender, LinePlotPoint e)
        {
            AddPoint(e.N, e.Line);
        }

        /// <summary>
        /// 添加点（线程安全）
        /// </summary>
        public void AddPoint(double value, int line)
        {
            if (line < 0 || line >= MaxLines)
                return;
            lock (_lock)
            {
                var buf = _buffers[line];
                int count = _counts[line];
                int head = _heads[line];
                if (count < MaxPointsPerLine)
                {
                    buf[count] = value;
                    _counts[line] = count + 1;
                }
                else
                {
                    buf[head] = value;
                    _heads[line] = (head + 1) % MaxPointsPerLine;
                }
            }
        }

        /// <summary>
        /// 获取某条线的当前数据副本（用于渲染）
        /// </summary>
        public void GetLineData(int line, double[] outX, double[] outY)
        {
            if (line < 0 || line >= MaxLines || outX == null || outY == null)
                return;
            lock (_lock)
            {
                var buf = _buffers[line];
                int count = _counts[line];
                int head = _heads[line];
                if (count < MaxPointsPerLine)
                {
                    for (int i = 0; i < count; i++)
                    {
                        outX[i] = i;
                        outY[i] = buf[i];
                    }
                }
                else
                {
                    for (int i = 0; i < MaxPointsPerLine; i++)
                    {
                        int idx = (head + i) % MaxPointsPerLine;
                        outX[i] = i;
                        outY[i] = buf[idx];
                    }
                }
            }
        }

        /// <summary>
        /// 获取某条线的当前点数
        /// </summary>
        public int GetLineCount(int line)
        {
            if (line < 0 || line >= MaxLines)
                return 0;
            lock (_lock)
            {
                return _counts[line];
            }
        }

        /// <summary>
        /// 清空所有数据
        /// </summary>
        public void Clear()
        {
            lock (_lock)
            {
                for (int i = 0; i < MaxLines; i++)
                {
                    _counts[i] = 0;
                    _heads[i] = 0;
                }
            }
        }

        /// <summary>
        /// 获取某条线的 Y 范围（用于自动缩放）
        /// </summary>
        public void GetLineRange(int line, out double min, out double max)
        {
            min = 0;
            max = 0;
            if (line < 0 || line >= MaxLines)
                return;
            lock (_lock)
            {
                int count = _counts[line];
                if (count == 0)
                    return;
                var buf = _buffers[line];
                int head = _heads[line];
                if (count < MaxPointsPerLine)
                {
                    min = max = buf[0];
                    for (int i = 1; i < count; i++)
                    {
                        double v = buf[i];
                        if (v < min) min = v;
                        if (v > max) max = v;
                    }
                }
                else
                {
                    min = max = buf[head];
                    for (int i = 1; i < MaxPointsPerLine; i++)
                    {
                        int idx = (head + i) % MaxPointsPerLine;
                        double v = buf[idx];
                        if (v < min) min = v;
                        if (v > max) max = v;
                    }
                }
            }
        }
    }
}
