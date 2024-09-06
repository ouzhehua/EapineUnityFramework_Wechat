#if UNITY_WEBGL || UNITY_EDITOR
using System;
using System.Net;
using System.Net.Sockets;
using VisionzFramework.Core;
using VisionzFramework.Core.Network;
using WeChatWASM;
using AddressFamily = VisionzFramework.Core.Network.AddressFamily;

namespace VisionzFramework.Runtime.WeChat
{
    public class UdpClientSocket : IUdpClientSocket, IDisposable
    {
        /// <summary>
        /// Socket 实例。
        /// </summary>
        private WXUDPSocket m_Socket;
        private UDPSocketSendOption m_UdpSocketSendOption;
        
        /// <summary>
        /// 网络地址类型。
        /// </summary>
        private AddressFamily m_AddressFamily;
        
        /// <summary>
        /// 绑定端口成功回调。
        /// </summary>
        public event UdpClientBindSuccessDelegate BindSuccessCallback;

        /// <summary>
        /// 发送数据成功回调。
        /// </summary>
        public event UdpClientSendSuccessDelegate SendSuccessCallback;

        /// <summary>
        /// 收到数据成功回调。
        /// </summary>
        public event UdpClientReceiveSuccessDelegate ReceiveSuccessCallback;

        /// <summary>
        /// 发生错误回调。
        /// </summary>
        public event UdpClientErrorDelegate UdpClientErrorCallback;
        
        private bool m_NeedRemoteInfo = false;
        private bool m_BindedPort = false;
        private bool m_HasOnMessage = false;
        private bool m_Disposed = false;
        
        public UdpClientSocket() : this(AddressFamily.IPv4) { }

        public UdpClientSocket(AddressFamily addressFamily) : this(addressFamily, null, null, null, null) { }

        public UdpClientSocket(AddressFamily addressFamily, UdpClientBindSuccessDelegate bindCallback, UdpClientSendSuccessDelegate clientSendSuccess, UdpClientReceiveSuccessDelegate clientReceiveCallBack, UdpClientErrorDelegate errorCallback)
        {
            m_AddressFamily = addressFamily;
            
            BindSuccessCallback = bindCallback;
            SendSuccessCallback = clientSendSuccess;
            ReceiveSuccessCallback = clientReceiveCallBack;
            UdpClientErrorCallback = errorCallback;

            InitSocket();
        }
        
        /// <summary>
        /// 初始化Socket
        /// </summary>
        private void InitSocket()
        {
            if (m_AddressFamily != AddressFamily.IPv4)
            {
                FrameworkLog.Error("Wechat AddressFamily, must be IPv4");
                m_AddressFamily = AddressFamily.IPv4;
            }

            m_Socket = WX.CreateUDPSocket();
            m_Socket.OnError(OnErrorCallback);
            m_UdpSocketSendOption = new UDPSocketSendOption(); //不new也能正常使用，不过还是先new了，以后感兴趣再研究为什么不需要new
        }

        /// <summary>
        /// 广播开关。
        /// </summary>
        public bool EnableBroadcast
        {
            get;
            set;
        }
        
        /// <summary>
        /// 监听收到的数据包是否需要RemoteInfo
        /// </summary>
        public bool NeedRemoteInfo
        {
            get
            {
                return m_NeedRemoteInfo;
            }
            set
            {
                m_NeedRemoteInfo = value;
                RefreshOnMessage();
            }
        }
        
        /// <summary>
        /// 绑定端口。
        /// </summary>
        /// <param name="port">端口号。</param>
        public void Bind(int port)
        {
            try
            {
                m_Socket.Bind(port);
            }
            catch (Exception exception)
            {
                if (UdpClientErrorCallback != null)
                {
                    SocketException socketException = exception as SocketException;
                    UdpClientErrorCallback(this, NetworkErrorCode.BindError, socketException != null ? socketException.SocketErrorCode : SocketError.SocketError, exception.ToString());
                }

                throw;
            }

            m_BindedPort = true;

            if (BindSuccessCallback != null)
            {
                BindSuccessCallback(port);
            }
            
            //启动接收
            RefreshOnMessage();
        }

        /// <summary>
        /// 发送数据。
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="offset"></param>
        /// <param name="size"></param>
        public void SendTo(byte[] buffer, int offset, int size, IPEndPoint remoteEP)
        {
            m_UdpSocketSendOption.address = remoteEP.Address.ToString();
            m_UdpSocketSendOption.port = remoteEP.Port;
            m_UdpSocketSendOption.message = buffer;
            m_UdpSocketSendOption.offset = offset;
            m_UdpSocketSendOption.length = size;
            m_UdpSocketSendOption.setBroadcast = EnableBroadcast;

            try
            {
                m_Socket.Send(m_UdpSocketSendOption);
            }
            catch (Exception exception)
            {
                if (UdpClientErrorCallback != null)
                {
                    SocketException socketException = exception as SocketException;
                    UdpClientErrorCallback(this, NetworkErrorCode.SendError, socketException != null ? socketException.SocketErrorCode : SocketError.Success, exception.ToString());
                }

                throw;
            }
            
            if (SendSuccessCallback != null)
            {
                SendSuccessCallback(size);
            }
        }

        private void RefreshOnMessage()
        {
            if (!m_BindedPort)
            {
                return;
            }

            if (m_HasOnMessage)
            {
                m_Socket.OffMessage(OnReceiveMessage);
            }

            m_Socket.OnMessage(OnReceiveMessage, NeedRemoteInfo);//暂时需要Info
            m_HasOnMessage = true;
        }
        
        private void OnReceiveMessage(UDPSocketOnMessageListenerResult result)
        {
            if (result == null)
            {
                FrameworkLog.Error("OnReceiveMessage Error : result is null");
                return;
            }

            FrameworkLog.Info($"OnReceiveMessage  {result.remoteInfo.address} {result.localInfo.address}");

            if (result.message == null || result.message.Length <= 0)
            {
                FrameworkLog.Error("OnReceiveMessage Error : message is null");
                return;
            }

            IPEndPoint remoteEP = null;
            if (NeedRemoteInfo)
            {
                remoteEP = IUdpClientSocket.IPv4Any;
                remoteEP.Address = IPAddress.Parse(result.remoteInfo.address);
                remoteEP.Port = result.remoteInfo.port;
            }
            
            //外部处理数据
            if (ReceiveSuccessCallback != null)
            {
                ReceiveSuccessCallback(result.message, 0, result.message.Length, remoteEP);
            }
        }

        private void OnErrorCallback(GeneralCallbackResult result)
        {
            if (UdpClientErrorCallback != null)
            {
                UdpClientErrorCallback(this, NetworkErrorCode.Unknown, SocketError.SocketError, result.errMsg);
            }
        }

        /// <summary>
        /// 关闭。
        /// </summary>
        public void Close()
        {
            if (m_Socket != null)
            {
                m_Socket.Close();
            }
            m_Socket = null;
            m_BindedPort = false;
        }
        
        /// <summary>
        /// 释放资源。
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        
        /// <summary>
        /// 释放资源。
        /// </summary>
        /// <param name="disposing">释放资源标记。</param>
        private void Dispose(bool disposing)
        {
            if (m_Disposed)
            {
                return;
            }

            if (disposing)
            {
                Close();
            }

            m_Disposed = true;
        }
    }
}
#endif