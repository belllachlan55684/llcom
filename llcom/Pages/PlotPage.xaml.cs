using ScottPlot;
using System;
using System.Collections.Generic;
using System.Linq;
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

namespace llcom.Pages
{
    /// <summary>
    /// PlotPage.xaml 的交互逻辑
    /// </summary>
    public partial class PlotPage : Page
    {
        public PlotPage()
        {
            InitializeComponent();
        }

        //最多十个图像
        private static int MaxPoints = 1000;
        // Ring Buffer：后台线程 O(1) 写入
        private double[][] ringBuffer = new double[10][];
        private int[] head = new int[10];
        private readonly object ringBufferLock = new object();
        // 供 ScottPlot 使用的显示数组，Render 前从 ringBuffer 同步
        private double[][] displayData = new double[10][];
        private double[] DataX = null;

        private ScottPlot.Plottable.Crosshair ch = null;

        private ScottPlot.Styles.IStyle[] Styles = ScottPlot.Style.GetStyles();
        private int StyleNow = -1;

        private bool NeedRefresh = true;
        private bool NeedDataRefresh = true;
        private bool NeedCrosshairRefresh = false;
        private DateTime lastCrosshairRenderTime = DateTime.MinValue;
        private const int CrosshairRenderIntervalMs = 120;
        private double pendingCrosshairX = 0;
        private double pendingCrosshairY = 0;
        private readonly object pendingCrosshairLock = new object();
        private System.Windows.Threading.DispatcherTimer crosshairTimer = null;

        bool first = true;
        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            if (!first)
                return;
            first = false;
            //暂时先定1000个点吧
            DataX = new double[MaxPoints];
            for (int i = 0; i < MaxPoints; i++)
                DataX[i] = i - MaxPoints + 1;
            for (int i = 0; i < 10; i++)
            {
                ringBuffer[i] = new double[MaxPoints];
                displayData[i] = new double[MaxPoints];
                Plot.Plot.AddSignalXY(DataX, displayData[i]);
            }
            Plot.Plot.SetAxisLimitsX(-MaxPoints, 0);
            ch = Plot.Plot.AddCrosshair(0,0);

            ch.Color = System.Drawing.Color.LightGray;
            ch.LineWidth = 2;

            // MouseMove 节流：Timer 每 80ms 更新十字光标并触发刷新
            crosshairTimer = new System.Windows.Threading.DispatcherTimer(
                System.Windows.Threading.DispatcherPriority.Background,
                Dispatcher)
            {
                Interval = TimeSpan.FromMilliseconds(80)
            };
            crosshairTimer.Tick += (s, ev) =>
            {
                double x, y;
                lock (pendingCrosshairLock)
                {
                    x = pendingCrosshairX;
                    y = pendingCrosshairY;
                }
                ch.X = x;
                ch.Y = y;
                NeedCrosshairRefresh = true;
                NeedRefresh = true;
            };
            crosshairTimer.Start();

            //定时刷吧，要不然卡
            new Thread(() =>
            {
                while (true)
                {
                    if (NeedRefresh)
                    {
                        bool shouldRender = false;
                        if (NeedDataRefresh)
                        {
                            shouldRender = true;
                        }
                        else if (NeedCrosshairRefresh)
                        {
                            var elapsed = (DateTime.Now - lastCrosshairRenderTime).TotalMilliseconds;
                            if (elapsed >= CrosshairRenderIntervalMs)
                            {
                                shouldRender = true;
                                lastCrosshairRenderTime = DateTime.Now;
                            }
                        }

                        if (shouldRender)
                        {
                            NeedRefresh = false;
                            NeedDataRefresh = false;
                            NeedCrosshairRefresh = false;
                            this.Dispatcher.BeginInvoke(new Action(delegate
                            {
                                try
                                {
                                    SyncRingBufferToDisplay();
                                    Plot.Render();
                                }
                                catch { }
                            }));
                        }
                        else if (NeedCrosshairRefresh)
                        {
                            NeedCrosshairRefresh = false;
                            NeedRefresh = false;
                        }
                    }
                    Thread.Sleep(100);
                    if (Tools.Global.isMainWindowsClosed)
                        return;
                }
            }).Start();

            LuaEnv.LuaApis.LinePlotAdd += (s, e) => {
                var n = e.N;
                var line = e.Line;
                Task.Run(() => AddPoint(n, line));
            };
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            lock (ringBufferLock)
            {
                for (int i = 0; i < 10; i++)
                {
                    for (int j = 0; j < MaxPoints; j++)
                        ringBuffer[i][j] = 0;
                    head[i] = 0;
                }
            }
            Refresh();
        }

        private void ThemeButton_Click(object sender, RoutedEventArgs e)
        {
            StyleNow++;
            if(StyleNow >= Styles.Length)
                StyleNow = 0;
            Plot.Plot.Style(Styles[StyleNow]);
            Refresh();
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            Plot.Plot.SetAxisLimitsX(-MaxPoints, 0);
            //防止最大值最小值错误（使用 displayData，Sync 在 Render 前执行，此处可能略旧但无妨）
            SyncRingBufferToDisplay();
            var min = displayData.Min(x => x.Min());
            var max = displayData.Max(x => x.Max());
            if(min < max)
                Plot.Plot.SetAxisLimitsY(min, max);
            Refresh();
        }

        private void Plot_MouseMove(object sender, MouseEventArgs e)
        {
            var p = Plot.GetMouseCoordinates();
            lock (pendingCrosshairLock)
            {
                pendingCrosshairX = p.x;
                pendingCrosshairY = p.y;
            }
        }

        private void Refresh() => NeedRefresh = NeedDataRefresh = true;

        /// <summary>
        /// 将 ringBuffer 按时间顺序复制到 displayData，供 ScottPlot 渲染。Render 前调用。
        /// </summary>
        private void SyncRingBufferToDisplay()
        {
            lock (ringBufferLock)
            {
                for (int line = 0; line < 10; line++)
                {
                    for (int i = 0; i < MaxPoints; i++)
                        displayData[line][i] = ringBuffer[line][(head[line] + 1 + i) % MaxPoints];
                }
            }
        }

        /// <summary>
        /// 后台线程调用，O(1) 写入 ringBuffer。
        /// </summary>
        private void AddPoint(double d, int line)
        {
            if (line >= 10)
                return;
            lock (ringBufferLock)
            {
                if (ringBuffer[line] == null)
                    ringBuffer[line] = new double[MaxPoints];
                head[line] = (head[line] + 1) % MaxPoints;
                ringBuffer[line][head[line]] = d;
            }
            Dispatcher.BeginInvoke(new Action(Refresh));
        }
    }
}
