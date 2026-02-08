using System.Net.Security;
using System.Net.Sockets;

namespace llcom.Model
{
    /// <summary>
    /// 共享的 Socket 异步状态对象，供 TcpLocalPage、UdpClientPage、TcpClientPage、TcpSslClientPage 使用
    /// </summary>
    public class StateObject
    {
        public Socket workSocket = null;
        public SslStream workStream = null;
        public const int BUFFER_SIZE = 204800;
        public byte[] buffer = new byte[BUFFER_SIZE];
        public bool isSSL = false;
    }

    /// <summary>
    /// Socket 或 SslStream 的封装，用于统一 Send/Close 操作
    /// </summary>
    public class SocketObj
    {
        Socket socket;
        SslStream sslStream;
        public SocketObj(Socket s)
        {
            socket = s;
        }
        public SocketObj(SslStream ssl)
        {
            sslStream = ssl;
        }
        public void Send(byte[] buff)
        {
            if (socket != null)
                socket.Send(buff);
            else if (sslStream != null)
                sslStream.Write(buff);
        }

        public void Close()
        {
            if (socket != null)
            {
                socket.Close();
                socket.Dispose();
            }
            else if (sslStream != null)
            {
                sslStream.Close();
                sslStream.Dispose();
            }
        }
    }
}
