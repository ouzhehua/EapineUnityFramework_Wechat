#if UNITY_WEBGL || UNITY_EDITOR
using System;
using System.Net;
using System.Net.Sockets;
using UnityEngine;
using VisionzFramework.Core;
using VisionzFramework.Core.Network;
using WeChatWASM;

namespace VisionzFramework.Runtime.WeChat
{
    public class TcpClientSocket : TcpClientSocketBase
    {
        /// <summary>
        /// Socket 实例。
        /// </summary>
        private WXTCPSocket m_Socket;
        private TCPSocketConnectOption m_TcpSocketConnectOption;

        private float m_ConnectTimeout = 2f;
        private string m_ConnectHost = null;
        private IPAddress m_ConnectIp = null;
        private int m_ConnectPort = 0;
        private bool m_Connecting = false;
        private bool m_Connected = false;
        
        public TcpClientSocket() : this(ITcpClientSocket.Size_512k) { }

        public TcpClientSocket(int bufferLength,
            TcpClientConnectSuccessDelegate connectSuccessCallback = null, TcpClientConnectFailureDelegate connectFailureCallback = null,
            TcpSocketSendSuccessDelegate sendSuccessCallback = null, TcpSocketFailureDelegate sendFailureCallback = null,
            TcpSocketReceiveSuccessDelegate receiveSuccessCallback = null, TcpSocketFailureDelegate receiveFailureCallback = null)
            : base(connectSuccessCallback, connectFailureCallback, sendSuccessCallback, sendFailureCallback, receiveSuccessCallback, receiveFailureCallback)
        {
            m_Socket = WX.CreateTCPSocket();
            m_Socket.OnConnect(OnConnectCallback);
            m_Socket.OnMessage(OnMessageCallback);
            m_Socket.OnError(OnErrorCallback);
            m_TcpSocketConnectOption = new TCPSocketConnectOption();
        }

        /// <summary>
        /// 获取是否已连接。
        /// </summary>
        public override bool Connected
        {
            get { return m_Connected; }
        }
        
        /// <summary>
        /// 连接服务器
        /// </summary>
        /// <param name="host">域名。</param>
        /// <param name="port">端口。</param>
        public override void Connect(string host, int port)
        {
            m_ConnectHost = host;
            m_ConnectPort = port;
            
            try
            {
                m_Connecting = true;
                m_TcpSocketConnectOption.address = host;
                m_TcpSocketConnectOption.port = port;
                m_TcpSocketConnectOption.timeout = m_ConnectTimeout;
                FrameworkLog.Info($"Connect host:{m_TcpSocketConnectOption.address} port:{m_TcpSocketConnectOption.port}");
                m_Socket.Connect(m_TcpSocketConnectOption);
            }
            catch (Exception exception)
            {
                m_Connecting = false;
                if (m_ConnectFailureCallback != null)
                {
                    SocketException socketException = exception as SocketException;
                    m_ConnectFailureCallback(this, m_ConnectHost, m_ConnectIp, m_ConnectPort,
                        socketException != null ? socketException.SocketErrorCode : SocketError.SocketError,
                        exception.ToString());
                }
                throw;
            }
        }

        /// <summary>
        /// 连接服务器。
        /// </summary>
        /// <param name="address">IP地址。</param>
        /// <param name="port">端口。</param>
        public override void Connect(IPAddress ipAddress, int port)
        {
            m_ConnectIp = ipAddress;
            m_ConnectPort = port;
            
            try
            {
                m_Connecting = true;
                m_TcpSocketConnectOption.address = ipAddress.ToString();
                m_TcpSocketConnectOption.port = port;
                m_TcpSocketConnectOption.timeout = m_ConnectTimeout;
                FrameworkLog.Info($"Connect ip:{m_TcpSocketConnectOption.address} port:{m_TcpSocketConnectOption.port}");
                m_Socket.Connect(m_TcpSocketConnectOption);
            }
            catch (Exception exception)
            {
                m_Connecting = false;
                if (m_ConnectFailureCallback != null)
                {
                    SocketException socketException = exception as SocketException;
                    m_ConnectFailureCallback(this, m_ConnectHost, m_ConnectIp, m_ConnectPort,
                        socketException != null ? socketException.SocketErrorCode : SocketError.SocketError,
                        exception.ToString());
                }
                throw;
            }
        }

        /// <summary>
        /// 连接服务器回调函数。
        /// </summary>
        /// <param name="asyncResult"></param>
        private void OnConnectCallback(GeneralCallbackResult result)
        {
            m_Connecting = false;
            
            if (string.IsNullOrEmpty(result.errMsg))
            {
                FrameworkLog.Info("OnConnectCallback");

                //处理外部回调
                if (m_ConnectSuccessCallback != null)
                {
                    m_ConnectSuccessCallback(this, m_ConnectHost, m_ConnectIp, m_ConnectPort);
                }
            }
            else
            {
                FrameworkLog.Error("OnConnectCallback " + result.errMsg);

                if (m_ConnectFailureCallback != null)
                {
                    m_ConnectFailureCallback(this, m_ConnectHost, m_ConnectIp, m_ConnectPort, SocketError.SocketError, result.errMsg);
                }
            }
        }

        /// <summary>
        /// 发送数据。
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="offset"></param>
        /// <param name="size"></param>
        public override void Send(byte[] buffer, int offset, int size)
        {
            try
            {
                byte[] sendData = new byte[size];
                Array.Copy(buffer, offset, sendData, 0, size);

                m_Socket.Write(sendData);
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

        /// <summary>
        /// 异步接收数据回调。
        /// </summary>
        /// <param name="asyncResult"></param>
        private void OnMessageCallback(TCPSocketOnMessageListenerResult result)
        {
            int bytesReceived = 0;
            if (result.message != null)
            {
                bytesReceived = result.message.Length;
            }

            if (bytesReceived <= 0)
            {
                FrameworkLog.Error("bytesReceived <= 0");
                if (m_ReceiveFailureCallback != null)
                {
                    m_ReceiveFailureCallback(this, SocketError.SocketError, "bytesReceived <= 0");    
                }
                return;
            }

            //外部处理数据
            if (m_ReceiveSuccessCallback != null)
            {
                m_ReceiveSuccessCallback(this, result.message, 0, bytesReceived);
            }
        }

        private void OnErrorCallback(GeneralCallbackResult result)
        {
            FrameworkLog.Error(result.errMsg);

            throw new FrameworkException("result.errMsg");
            
            //m_ConnectFailureCallback
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
                m_TcpSocketConnectOption = null;
                //释放托管资源
            }
            //释放非托管资源
            m_ConnectIp = null;
            
            //释放父类
            base.Dispose(disposing);

            m_Disposed = true;
        }
    }
}
#endif