#if UNITY_WEBGL || UNITY_EDITOR
using System;
using System.Net;
using System.Net.Sockets;
using UnityEngine;
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
        /// </summary>
        public event TcpSendCallbackDelegate SendCallback;

        /// <summary>
        /// 外部收到数据回调。
        /// </summary>
        public event TcpReceiveCallbackDelegate ReceiveCallback;

        /// <summary>
        /// 发生错误回调事件。
        /// </summary>
        public event Action<ITcpClientSocket, SocketError, string> ErrorCallback;

        private float m_ConnectTimeout = 2f;


        public TcpClientSocket() : this(ITcpClientSocket.Size_512k) { }

        public TcpClientSocket(int bufferLength) : this(bufferLength, null, null, null, null) { }

        public TcpClientSocket(int bufferLength, System.Action connectCallback, TcpSendCallbackDelegate sendCallback, TcpReceiveCallbackDelegate receiveCallBack, Action<ITcpClientSocket, SocketError, string> errorCallback)
        {
            ConnectCallback = connectCallback;
            SendCallback = sendCallback;
            ReceiveCallback = receiveCallBack;
            ErrorCallback = errorCallback;

            m_Socket = WX.CreateTCPSocket();
            m_Socket.OnConnect(OnConnectCallback);
            m_Socket.OnMessage(OnMessageCallback);
            m_Socket.OnError(OnErrorCallback);

            m_TCPSocketConnectOption = new TCPSocketConnectOption();
        }

        /// <summary>
        /// 连接服务器
        /// </summary>
        /// <param name="host">域名。</param>
        /// <param name="port">端口。</param>
        public void Connect(string host, int port)
        {
            try
            {
                m_TCPSocketConnectOption.address = host;
                m_TCPSocketConnectOption.port = port;
                m_TCPSocketConnectOption.timeout = m_ConnectTimeout;
                Debug.Log($"Connect host:{m_TCPSocketConnectOption.address} port:{m_TCPSocketConnectOption.port}");
                m_Socket.Connect(m_TCPSocketConnectOption);
            }
            catch (Exception exception)
            {
                throw exception;
            }
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
                m_TCPSocketConnectOption.timeout = m_ConnectTimeout;
                Debug.Log($"Connect ip:{m_TCPSocketConnectOption.address} port:{m_TCPSocketConnectOption.port}");
                m_Socket.Connect(m_TCPSocketConnectOption);
            }
            catch (Exception exception)
            {
                throw exception;
            }
        }

        /// <summary>
        /// 连接服务器回调函数。
        /// </summary>
        /// <param name="asyncResult"></param>
        private void OnConnectCallback(GeneralCallbackResult result)
        {
            if (string.IsNullOrEmpty(result.errMsg))
            {
                Debug.Log("OnConnectCallback");

                //处理外部回调
                if (ConnectCallback != null)
                {
                    ConnectCallback();
                }
            }
            else
            {
                Debug.LogError("OnConnectCallback " + result.errMsg);

                if (ErrorCallback != null)
                {
                    ErrorCallback(this, SocketError.ConnectionAborted, result.errMsg);
                }
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
                if (ErrorCallback != null)
                {
                    ErrorCallback(this, SocketError.SocketError, e.ToString());
                }
                throw;
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
                //Close();
                return;
            }

            //外部处理数据
            if (ReceiveCallback != null)
            {
                ReceiveCallback(result.message, 0, bytesReceived);
            }
        }

        private void OnErrorCallback(GeneralCallbackResult result)
        {
            if (ErrorCallback != null)
            {
                ErrorCallback(this, SocketError.SocketError, result.errMsg);
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