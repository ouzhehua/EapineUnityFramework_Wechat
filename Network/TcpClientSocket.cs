#if UNITY_WEBGL || UNITY_EDITOR
using System;
using System.Net;
using VisionzFramework.Core.Network;
using WeChatWASM;

namespace VisionzFramework.Runtime.WeChat
{
    public class TcpClientSocket : ITcpClientSocket
    {
        /// <summary>
        /// Socket 实例。
        /// </summary>
        private WXTCPSocket m_Socket;
        private TCPSocketConnectOption m_TCPSocketConnectOption;

        /// <summary>
        /// 外部连接成功回调。
        /// </summary>
        public event Action ConnectCallback;

        /// <summary>
        /// 外部发送数据回调。
        /// <param name="sendLength">发送数据长度。</param>
        /// </summary>
        public event Action<int> SendCallback;

        /// <summary>
        /// 外部收到数据回调。
        /// </summary>
        public event Action<byte[], int, int> ReceiveCallback;


        private Action<string> m_ErrorEvent;

        public TcpClientSocket() : this(ITcpClientSocket.Size_512k) { }

        public TcpClientSocket(int bufferLength) : this(bufferLength, null, null, null) { }

        public TcpClientSocket(int bufferLength, System.Action connectCallback, Action<int> sendCallback, Action<byte[], int, int> receiveCallBack)
        {
            ConnectCallback = connectCallback;
            SendCallback = sendCallback;
            ReceiveCallback = receiveCallBack;

            m_Socket = WX.CreateTCPSocket();
            m_Socket.OnConnect(ConnectAsyncCallback);
            m_Socket.OnMessage(ReceiveAsyncCallback);
            //m_Socket.OnError()
        }

        /// <summary>
        /// 连接服务器
        /// </summary>
        /// <param name="host">域名。</param>
        /// <param name="port">端口。</param>
        public void Connect(string host, int port)
        {
            IPAddress ipAddress = null;

            try
            {
                IPAddress[] addresses = Dns.GetHostEntry(host).AddressList;
                foreach (var item in addresses)
                {
                    if (item.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    {
                        ipAddress = item;
                        break;
                    }
                }
            }
            catch (Exception e)
            {
                if (m_ErrorEvent != null)
                {
                    m_ErrorEvent(e.ToString());
                }
                return;
            }

            if (ipAddress == null)
            {
                throw new Exception("can not parse host : " + host);
            }

            Connect(ipAddress, port);
        }

        /// <summary>
        /// 连接服务器。
        /// </summary>
        /// <param name="address">IP地址。</param>
        /// <param name="port">端口。</param>
        public void Connect(IPAddress ipAddress, int port)
        {
            try
            {
                m_TCPSocketConnectOption.address = ipAddress.ToString();
                m_TCPSocketConnectOption.port = port;
                m_TCPSocketConnectOption.timeout = 2;

                m_Socket.Connect(m_TCPSocketConnectOption);
            }
            catch (Exception e)
            {
                throw;
            }
        }

        /// <summary>
        /// 连接服务器回调函数。
        /// </summary>
        /// <param name="asyncResult"></param>
        private void ConnectAsyncCallback(GeneralCallbackResult result)
        {
            //result.errMsg

            //处理外部回调
            if (ConnectCallback != null)
            {
                ConnectCallback();
            }
        }

        /// <summary>
        /// 发送数据。
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="offset"></param>
        /// <param name="size"></param>
        public void Send(byte[] buffer, int offset, int size)
        {
            try
            {
                byte[] sendData = new byte[size];
                Array.Copy(buffer, offset, sendData, 0, size);

                m_Socket.Write(sendData);

                if (SendCallback != null)
                {
                    SendCallback(size);
                }
            }
            catch (Exception e)
            {
                if (m_ErrorEvent != null)
                {
                    m_ErrorEvent(e.ToString());
                }
                throw;
            }
        }

        /// <summary>
        /// 异步接收数据回调。
        /// </summary>
        /// <param name="asyncResult"></param>
        private void ReceiveAsyncCallback(TCPSocketOnMessageListenerResult result)
        {
            int bytesReceived = 0;
            if (result.message != null)
            {
                bytesReceived = result.message.Length;
            }

            if (bytesReceived <= 0)
            {
                //Close();
                return;
            }

            //外部处理数据
            if (ReceiveCallback != null)
            {
                ReceiveCallback(result.message, 0, bytesReceived);
            }
        }

        public void Close()
        {
            if (m_Socket != null)
            {
                m_Socket.Close();
            }
            m_Socket = null;
        }
    }
}
#endif