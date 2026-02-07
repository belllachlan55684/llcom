using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Windows;

namespace llcom.Tools
{
    /// <summary>
    /// 供串口、Socket 等通道共用的 send_convert / recv_convert 脚本调用辅助
    /// </summary>
    public static class LuaConvertHelper
    {
        /// <summary>
        /// 发送前转换：按顺序对原始数据执行 send_convert 脚本链
        /// </summary>
        /// <param name="data">待发送的原始数据</param>
        /// <param name="interfaceKey">数据接口键（Serial/TcpClient/TcpLocal 等），null 时使用全局 sendScript</param>
        /// <param name="isHex">保留参数，兼容调用方，已忽略</param>
        /// <returns>转换后的数据，失败时返回 null</returns>
        public static byte[] ApplySendConvert(byte[] data, string interfaceKey = null, bool? isHex = null)
        {
            if (data == null || data.Length == 0)
                return data;
            var scripts = string.IsNullOrEmpty(interfaceKey)
                ? new List<string> { Global.setting.sendScript }
                : Global.setting.GetSendScriptListForInterface(interfaceKey);
            if (scripts == null || scripts.Count == 0)
                return data;
            try
            {
                byte[] current = data;
                foreach (var scriptName in scripts)
                {
                    if (string.IsNullOrEmpty(scriptName)) continue;
                    var path = Global.ProfilePath + $"user_script_send_convert/{scriptName}.lua";
                    if (!File.Exists(path))
                        continue;
                    var result = LuaEnv.LuaLoader.Run(
                        $"{scriptName}.lua",
                        new ArrayList { "uartData", current });
                    if (result == null)
                        return null;
                    current = result;
                }
                return current ?? new byte[0];
            }
            catch (Exception ex)
            {
                MessageBox.Show($"{Application.Current.TryFindResource("ErrorScript") as string ?? "?!"}\r\n{ex}");
                return null;
            }
        }

        /// <summary>
        /// 接收后转换：按顺序对接收数据执行 recv_convert 脚本链
        /// </summary>
        /// <param name="data">接收到的原始数据</param>
        /// <param name="uartPara">可选，recv 脚本参数（快捷发送区等场景）</param>
        /// <param name="uartSendRaw">可选，发送前的原始数据（快捷发送区等场景）</param>
        /// <param name="interfaceKey">数据接口键（Serial/TcpClient 等），null 时回退到 Serial</param>
        /// <returns>转换后的数据，失败时返回 null，脚本返回空时返回空数组</returns>
        public static byte[] ApplyRecvConvert(byte[] data, byte[] uartPara = null, byte[] uartSendRaw = null, string interfaceKey = null)
        {
            if (data == null)
                return null;
            var scripts = Global.GetEffectiveRecvScriptListForInterface(interfaceKey ?? "Serial");
            if (scripts == null || scripts.Count == 0)
                return (byte[])data.Clone();
            try
            {
                var para = uartPara ?? new byte[0];
                var sendRaw = uartSendRaw ?? new byte[0];
                var backup = Global.recvPara;
                Global.recvPara = new byte[][] { para, sendRaw };
                try
                {
                    byte[] current = data;
                    foreach (var scriptName in scripts)
                    {
                        if (string.IsNullOrEmpty(scriptName)) continue;
                        var path = Global.ProfilePath + $"user_script_recv_convert/{scriptName}.lua";
                        if (!File.Exists(path))
                            continue;
                        var result = LuaEnv.LuaLoader.Run(
                            $"{scriptName}.lua",
                            new ArrayList { "uartData", current, "uartPara", para, "uartSendRaw", sendRaw },
                            "user_script_recv_convert/");
                        if (result == null)
                            return null;
                        current = result;
                    }
                    return current ?? new byte[0];
                }
                finally
                {
                    Global.recvPara = backup;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"receive convert lua script error\r\n{ex}");
                return null;
            }
        }
    }
}
