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
    public class UdpClientSocket : UdpClientSocketBase
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
        
        private int m_BindPort = 0;
        private bool m_BindedPort = false;
        private bool m_NeedRemoteInfo = false;
        private bool m_HasOnMessage = false;
        
        public UdpClientSocket() : this(AddressFamily.IPv4) { }
        
        public UdpClientSocket(AddressFamily addressFamily,
            UdpClientBindSuccessDelegate bindSuccessCallback = null, UdpClientBindFailureDelegate bindFailureCallback = null,
            UdpClientSendSuccessDelegate sendSuccessCallback = null, UdpClientFailureDelegate sendFailureCallback = null,
            UdpClientReceiveSuccessDelegate receiveSuccessCallback = null, UdpClientFailureDelegate receiveFailureCallback = null)
            : base(bindSuccessCallback, bindFailureCallback, sendSuccessCallback, sendFailureCallback, receiveSuccessCallback, receiveFailureCallback)
        {
            m_AddressFamily = addressFamily;
            
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
        public override bool EnableBroadcast
        {
            get;
            set;
        }
        
        /// <summary>
        /// 监听收到的数据包是否需要RemoteInfo
        /// </summary>
        public override bool NeedRemoteInfo
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
        public override void Bind(int port)
        {
            m_BindPort = port;
            try
            {
                m_Socket.Bind(port);
            }
            catch (Exception exception)
            {
                if (m_BindFailureCallback != null)
                {
                    m_BindFailureCallback(this, port, SocketError.SocketError, exception.ToString());
                }

                throw;
            }

            m_BindedPort = true;

            if (m_BindSuccessCallback != null)
            {
                m_BindSuccessCallback(this, port);
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
        public override void SendTo(byte[] buffer, int offset, int size, IPEndPoint remoteEP)
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
                if (m_SendFailureCallback != null)
                {
                    m_SendFailureCallback(this, SocketError.SocketError, exception.ToString());
                }
                throw;
            }
            
            if (m_SendSuccessCallback != null)
            {
                m_SendSuccessCallback(this, size);
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
                if (m_ReceiveFailureCallback != null)
                {
                    m_ReceiveFailureCallback(this,  SocketError.SocketError, "OnReceiveMessage Error : result is null");
                }
                return;
            }

            FrameworkLog.Info($"OnReceiveMessage  {result.remoteInfo.address} {result.localInfo.address}");

            if (result.message == null || result.message.Length <= 0)
            {
                if (m_ReceiveFailureCallback != null)
                {
                    m_ReceiveFailureCallback(this,  SocketError.SocketError, "OnReceiveMessage Error : message is null");
                }
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
            if (m_ReceiveSuccessCallback != null)
            {
                m_ReceiveSuccessCallback(this, result.message, 0, result.message.Length, remoteEP);
            }
        }

        private void OnErrorCallback(GeneralCallbackResult result)
        {
            FrameworkLog.Error(result.errMsg);

            throw new FrameworkException("result.errMsg");
            
            //m_BindFailureCallback
            //m_SendFailureCallback
            //m_ReceiveFailureCallback
        }

        /// <summary>
        /// 关闭。
        /// </summary>
        public override void Close()
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
        public override void Dispose()
        {
            try
            {
                Dispose(true);
            }
            finally
            {
                base.Dispose();    
            }
        }

        private bool m_Disposed = false;
        /// <summary>
        /// 释放资源。
        /// </summary>
        /// <param name="disposing">释放资源标记。</param>
        protected override void Dispose(bool disposing)
        {
            if (m_Disposed)
            {
                return;
            }

            if (disposing)
            {
                //释放托管资源
            }
            //释放非托管资源
            m_UdpSocketSendOption = null;
            
            //释放父类
            base.Dispose(disposing);

            m_Disposed = true;
        }
    }
}
#endif