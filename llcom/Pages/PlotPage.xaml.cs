using ScottPlot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace llcom.Pages
{
    /// <summary>
    /// PlotPage.xaml 的交互逻辑
    /// 使用 ScottPlot 5 DataStreamer 实现实时流，减少 SyncRingBufferToDisplay 等开销
    /// </summary>
    public partial class PlotPage : Page
    {
        public PlotPage()
        {
            InitializeComponent();
        }

        private static int MaxPoints = 1000;
        private ScottPlot.Plottables.DataStreamer[] streamers = new ScottPlot.Plottables.DataStreamer[10];

        private dynamic ch = null;

        private static readonly ScottPlot.IPalette[] Palettes = new ScottPlot.IPalette[]
        {
            new ScottPlot.Palettes.Category10(),
            new ScottPlot.Palettes.Nord(),
            new ScottPlot.Palettes.Penumbra(),
            new ScottPlot.Palettes.ColorblindFriendly(),
        };
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

            for (int i = 0; i < 10; i++)
            {
                var s = Plot.Plot.Add.DataStreamer(MaxPoints);
                s.ViewScrollLeft();
                streamers[i] = s;
            }
            Plot.Plot.Axes.SetLimitsX(-MaxPoints, 0);

            ch = Plot.Plot.Add.Crosshair(0, 0);
            ch.LineColor = ScottPlot.Colors.LightGray;
            ch.LineWidth = 2;

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
                ch.Position = new ScottPlot.Coordinates(x, y);
                NeedCrosshairRefresh = true;
                NeedRefresh = true;
            };
            crosshairTimer.Start();

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
                                    Plot.Refresh();
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
            for (int i = 0; i < 10; i++)
            {
                streamers[i]?.Clear(0);
            }
            Refresh();
        }

        private void ThemeButton_Click(object sender, RoutedEventArgs e)
        {
            StyleNow++;
            if (StyleNow >= Palettes.Length)
                StyleNow = 0;
            Plot.Plot.Add.Palette = Palettes[StyleNow];
            Refresh();
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            Plot.Plot.Axes.SetLimitsX(-MaxPoints, 0);
            double min = double.MaxValue, max = double.MinValue;
            for (int i = 0; i < 10; i++)
            {
                var s = streamers[i];
                if (s != null)
                {
                    min = Math.Min(min, s.Data.DataMin);
                    max = Math.Max(max, s.Data.DataMax);
                }
            }
            if (min < max)
                Plot.Plot.Axes.SetLimitsY(min, max);
            Refresh();
        }

        private void Plot_MouseMove(object sender, MouseEventArgs e)
        {
            var pos = e.GetPosition(Plot);
            float px = (float)pos.X, py = (float)pos.Y;
            if (Plot.DisplayScale != 1.0)
            {
                px *= (float)Plot.DisplayScale;
                py *= (float)Plot.DisplayScale;
            }
            var coords = Plot.Plot.GetCoordinates(new ScottPlot.Pixel(px, py));
            lock (pendingCrosshairLock)
            {
                pendingCrosshairX = coords.X;
                pendingCrosshairY = coords.Y;
            }
        }

        private void Refresh() => NeedRefresh = NeedDataRefresh = true;

        private void AddPoint(double d, int line)
        {
            if (line >= 10 || line < 0)
                return;
            var s = streamers[line];
            if (s != null)
            {
                s.Add(d);
                Dispatcher.BeginInvoke(new Action(Refresh));
            }
        }
    }
}
