using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;
using llcom.Tools;

namespace llcom.View
{
    /// <summary>
    /// 系统日志窗口：订阅 SystemLog.MessageReceived，在 UI 线程追加到列表；限制最大行数，Closed 时取消订阅。
    /// </summary>
    public partial class SystemLogWindow : Window
    {
        private const int MaxLogLines = 5000;
        private EventHandler<SystemLog.MessageEventArgs> _messageHandler;

        public ObservableCollection<string> LogLines { get; } = new ObservableCollection<string>();

        public SystemLogWindow()
        {
            InitializeComponent();
            DataContext = this;
            Loaded += SystemLogWindow_Loaded;
            Closed += SystemLogWindow_Closed;
        }

        private void SystemLogWindow_Loaded(object sender, RoutedEventArgs e)
        {
            _messageHandler = (s, ev) =>
            {
                Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() => AddLine(ev.Message)));
            };
            SystemLog.MessageReceived += _messageHandler;
        }

        private void SystemLogWindow_Closed(object sender, EventArgs e)
        {
            if (_messageHandler != null)
            {
                SystemLog.MessageReceived -= _messageHandler;
                _messageHandler = null;
            }
        }

        private void AddLine(string message)
        {
            if (string.IsNullOrEmpty(message))
                return;
            LogLines.Add(message);
            while (LogLines.Count > MaxLogLines)
                LogLines.RemoveAt(0);
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            LogLines.Clear();
        }
    }
}
