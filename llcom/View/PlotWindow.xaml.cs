using ScottPlot;
using System;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using llcom.Tools;

namespace llcom.View
{
    /// <summary>
    /// 独立曲线窗口：从 PlotDataReceiver 读取数据显示。
    /// 后台线程同步数据，UI 线程渲染（Dispatcher.BeginInvoke 低优先级）。
    /// </summary>
    public partial class PlotWindow : Window
    {
        private const int MaxPoints = PlotDataReceiver.MaxPointsPerLine;
        private ScottPlot.Plottables.DataStreamer[] _streamers = new ScottPlot.Plottables.DataStreamer[PlotDataReceiver.MaxLines];
        private dynamic _crosshair;
        private static readonly ScottPlot.IPalette[] Palettes = new ScottPlot.IPalette[]
        {
            new ScottPlot.Palettes.Category10(),
            new ScottPlot.Palettes.Nord(),
            new ScottPlot.Palettes.Penumbra(),
            new ScottPlot.Palettes.ColorblindFriendly(),
        };
        private int _styleNow = -1;

        private volatile bool _needRefresh = true;
        private volatile bool _needDataRefresh = true;
        private bool _needCrosshairRefresh = false;
        private DateTime _lastCrosshairRenderTime = DateTime.MinValue;
        private const int CrosshairRenderIntervalMs = 120;
        private double _pendingCrosshairX, _pendingCrosshairY;
        private readonly object _pendingCrosshairLock = new object();
        private System.Windows.Threading.DispatcherTimer _crosshairTimer;
        private System.Windows.Threading.DispatcherTimer _syncTimer;

        private readonly double[] _tempX = new double[MaxPoints];
        private readonly double[] _tempY = new double[MaxPoints];

        public PlotWindow()
        {
            InitializeComponent();
            Loaded += PlotWindow_Loaded;
            Closed += PlotWindow_Closed;
        }

        private void PlotWindow_Loaded(object sender, RoutedEventArgs e)
        {
            var receiver = PlotDataReceiver.Instance;

            for (int i = 0; i < PlotDataReceiver.MaxLines; i++)
            {
                var s = Plot.Plot.Add.DataStreamer(MaxPoints);
                s.ViewScrollLeft();
                _streamers[i] = s;
            }
            Plot.Plot.Axes.SetLimitsX(-MaxPoints, 0);

            _crosshair = Plot.Plot.Add.Crosshair(0, 0);
            _crosshair.LineColor = ScottPlot.Colors.LightGray;
            _crosshair.LineWidth = 2;

            _crosshairTimer = new System.Windows.Threading.DispatcherTimer(
                System.Windows.Threading.DispatcherPriority.Background,
                Dispatcher)
            {
                Interval = TimeSpan.FromMilliseconds(80)
            };
            _crosshairTimer.Tick += (s, ev) =>
            {
                lock (_pendingCrosshairLock)
                {
                    _crosshair.Position = new ScottPlot.Coordinates(_pendingCrosshairX, _pendingCrosshairY);
                }
                _needCrosshairRefresh = true;
                _needRefresh = true;
            };
            _crosshairTimer.Start();

            _syncTimer = new System.Windows.Threading.DispatcherTimer(
                System.Windows.Threading.DispatcherPriority.Background,
                Dispatcher)
            {
                Interval = TimeSpan.FromMilliseconds(100)
            };
            _syncTimer.Tick += SyncTimer_Tick;
            _syncTimer.Start();

            new Thread(RenderLoop)
            {
                IsBackground = true
            }.Start();
        }

        private void PlotWindow_Closed(object sender, EventArgs e)
        {
            _syncTimer?.Stop();
            _crosshairTimer?.Stop();
        }

        private void SyncTimer_Tick(object sender, EventArgs e)
        {
            SyncFromReceiver();
        }

        private void SyncFromReceiver()
        {
            var receiver = PlotDataReceiver.Instance;
            for (int line = 0; line < PlotDataReceiver.MaxLines; line++)
            {
                var s = _streamers[line];
                if (s == null) continue;
                int count = receiver.GetLineCount(line);
                if (count == 0) continue;
                receiver.GetLineData(line, _tempX, _tempY);
                s.Clear(0);
                for (int i = 0; i < count; i++)
                    s.Add(_tempY[i]);
            }
            _needRefresh = true;
            _needDataRefresh = true;
        }

        private void RenderLoop()
        {
            while (true)
            {
                if (Tools.Global.isMainWindowsClosed)
                    return;
                if (_needRefresh)
                {
                    bool shouldRender = false;
                    if (_needDataRefresh)
                        shouldRender = true;
                    else if (_needCrosshairRefresh)
                    {
                        var elapsed = (DateTime.Now - _lastCrosshairRenderTime).TotalMilliseconds;
                        if (elapsed >= CrosshairRenderIntervalMs)
                        {
                            shouldRender = true;
                            _lastCrosshairRenderTime = DateTime.Now;
                        }
                    }

                    if (shouldRender)
                    {
                        _needRefresh = false;
                        _needDataRefresh = false;
                        _needCrosshairRefresh = false;
                        try
                        {
                            Dispatcher.BeginInvoke(
                                new Action(() =>
                                {
                                    try { Plot.Refresh(); }
                                    catch { }
                                }),
                                System.Windows.Threading.DispatcherPriority.Background);
                        }
                        catch { }
                    }
                    else if (_needCrosshairRefresh)
                    {
                        _needCrosshairRefresh = false;
                        _needRefresh = false;
                    }
                }
                Thread.Sleep(100);
            }
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            PlotDataReceiver.Instance.Clear();
            for (int i = 0; i < PlotDataReceiver.MaxLines; i++)
                _streamers[i]?.Clear(0);
            RefreshPlot();
        }

        private void ThemeButton_Click(object sender, RoutedEventArgs e)
        {
            _styleNow++;
            if (_styleNow >= Palettes.Length) _styleNow = 0;
            Plot.Plot.Add.Palette = Palettes[_styleNow];
            RefreshPlot();
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            Plot.Plot.Axes.SetLimitsX(-MaxPoints, 0);
            double min = double.MaxValue, max = double.MinValue;
            var receiver = PlotDataReceiver.Instance;
            for (int i = 0; i < PlotDataReceiver.MaxLines; i++)
            {
                if (receiver.GetLineCount(i) == 0) continue;
                receiver.GetLineRange(i, out double mn, out double mx);
                min = Math.Min(min, mn);
                max = Math.Max(max, mx);
            }
            if (min < max)
                Plot.Plot.Axes.SetLimitsY(min, max);
            RefreshPlot();
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
            lock (_pendingCrosshairLock)
            {
                _pendingCrosshairX = coords.X;
                _pendingCrosshairY = coords.Y;
            }
        }

        private void RefreshPlot() => _needRefresh = _needDataRefresh = true;
    }
}
