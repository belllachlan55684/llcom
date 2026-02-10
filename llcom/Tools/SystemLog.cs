using System;

namespace llcom.Tools
{
    /// <summary>
    /// 系统日志：静态 API，供程序任意位置向系统日志窗口打印消息。线程安全，由窗口订阅并 marshall 到 UI 线程。
    /// </summary>
    public static class SystemLog
    {
        /// <summary>
        /// 消息参数，用于事件传递字符串
        /// </summary>
        public sealed class MessageEventArgs : EventArgs
        {
            public string Message { get; }
            public MessageEventArgs(string message) => Message = message ?? string.Empty;
        }

        /// <summary>
        /// 当有新的系统日志消息时触发。可在任意线程触发，订阅方需在 UI 线程更新（如 Dispatcher.BeginInvoke）。
        /// </summary>
        public static event EventHandler<MessageEventArgs> MessageReceived;

        /// <summary>
        /// 向系统日志输出一行。可从任意线程调用；若已打开系统日志窗口，将在其 UI 线程追加显示。
        /// </summary>
        public static void WriteLine(string message)
        {
            MessageReceived?.Invoke(null, new MessageEventArgs(message ?? string.Empty));
        }
    }
}
