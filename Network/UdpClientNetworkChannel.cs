using System;
using System.Net;
using System.Net.Sockets;
using VisionzFramework.Core;
using VisionzFramework.Core.Network;
using WeChatWASM;

namespace VisionzFramework.Runtime.WeChat
{
    /// <summary>
    /// UDP Client网络频道。
    /// 参考 System.Net.Sockets.UdpClient;
    /// </summary>
    public class UdpClientNetworkChannel : UdpClientNetworkChannelBase
    {
        private WXUDPSocket m_Socket;
        private UDPSocketSendOption m_UdpSocketSendOption;

        //当前数据包
        private UdpPacket m_CurrentPacket;

        //发数据缓冲区
        private byte[] m_SendBuffer = new byte[MaxUDPSize];

        private bool m_NeedRemoteInfo = false;
        private bool m_BindedPort = false;
        private bool m_HasOnMessage = false;


        /// <summary>
        /// 初始化网络频道的新实例。
        /// </summary>
        /// <param name="name">网络频道名称。</param>
        /// <param name="networkChannelHelper">网络频道辅助器。</param>
        public UdpClientNetworkChannel(string name, Packet.PacketFlagType packetIdType, Core.Network.AddressFamily addressFamily) : base(name, packetIdType, addressFamily)
        {
            InitSocket();
        }

        /// <summary>
        /// 初始化Socket
        /// </summary>
        private void InitSocket()
        {
            if (m_Socket != null)
            {
                return;
            }

            m_Socket = WX.CreateUDPSocket();
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

        /// <summary>
        /// 绑定端口。
        /// </summary>
        /// <param name="port">端口号。</param>
        public override void Bind(int port)
        {
            m_Socket.Bind(port);

            m_BindedPort = true;

            //启动接收
            RefreshOnMessage();
        }

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
        /// 发送数据包。
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="packet"></param>
        public override void SendTo<T>(T packet)
        {
            if (m_Socket == null)
            {
                string errorMessage = "Socket is null.";
                if (NetworkChannelError != null)
                {
                    NetworkChannelError(this, NetworkErrorCode.SendError, SocketError.Success, errorMessage);
                    return;
                }

                throw new FrameworkException(errorMessage);
            }

            if (packet == null)
            {
                string errorMessage = "Packet is invalid.";
                if (NetworkChannelError != null)
                {
                    NetworkChannelError(this, NetworkErrorCode.SendError, SocketError.Success, errorMessage);
                    return;
                }

                throw new FrameworkException(errorMessage);
            }

            lock (m_SendPacketPool)
            {
                m_SendPacketPool.Enqueue(packet);
            }

            ProcessSend();
        }

        private void ProcessSend()
        {
            if (m_Socket == null)
            {
                return;
            }

            if (m_CurrentPacket != null)
            {
                return;//有数据包在发
            }

            Packet packet = null;
            lock (m_SendPacketPool)
            {
                if (m_SendPacketPool.Count == 0)
                {
                    return;
                }

                packet = m_SendPacketPool.Dequeue();
            }

            if (packet == null)
            {
                FrameworkLog.Error("Data Error");
                ProcessSend();
            }
            else
            {
                m_CurrentPacket = packet as UdpPacket;
                if (m_CurrentPacket == null)
                {
                    FrameworkLog.Error("Data Error");
                    ProcessSend();
                }
                else
                {
                    bool showLog = false;
                    if (showLog)
                    {
                        System.Threading.Thread currentThread = System.Threading.Thread.CurrentThread;
                        FrameworkLog.Info($"{Name} ProcessSend id:{m_CurrentPacket.Id} to:{m_CurrentPacket.endPoint} type:{m_CurrentPacket.GetType()} Thread ID:{currentThread.ManagedThreadId} IsThreadPoolThread:{currentThread.IsThreadPoolThread} ThreadState:{currentThread.ThreadState}");
                    }

                    int size = m_CurrentPacket.Serialize(m_SendBuffer, 0);//写入Body数据,返回Body长度

                    m_UdpSocketSendOption.address = m_CurrentPacket.endPoint.Address.ToString();
                    m_UdpSocketSendOption.port = m_CurrentPacket.endPoint.Port;
                    m_UdpSocketSendOption.message = m_SendBuffer;
                    m_UdpSocketSendOption.offset = 0;
                    m_UdpSocketSendOption.length = size;
                    m_UdpSocketSendOption.setBroadcast = EnableBroadcast;

                    try
                    {
                        m_Socket.Send(m_UdpSocketSendOption);
                    }
                    catch (Exception exception)
                    {
                        if (NetworkChannelError != null)
                        {
                            SocketException socketException = exception as SocketException;
                            NetworkChannelError(this, NetworkErrorCode.SendError, socketException != null ? socketException.SocketErrorCode : SocketError.Success, exception.ToString());
                        }

                        throw;
                    }
                    finally
                    {
                        lock (m_CurrentPacket)
                        {
                            ReferencePool.Release(m_CurrentPacket);
                            m_CurrentPacket = null;
                        }

                        m_SentPacketCount++;
                        ProcessSend();
                    }
                }
            }
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
                remoteEP = IPv4Any;
                remoteEP.Address = IPAddress.Parse(result.remoteInfo.address);
                remoteEP.Port = result.remoteInfo.port;
            }
            
            //处理数据
            int packetId = Packet.DeserializePacketId(result.message, 0, m_PacketIdType);
            int subscribeCount = m_ReceiveEventPool.GetSubscribeCount(packetId);
            if (subscribeCount <= 0)
            {
                FrameworkLog.Warning($"{Name} receive uncase message : {packetId}");
            }
            else
            {
                Type packetType = GetReceivePacketType(packetId);
                if (packetType == null)
                {
                    FrameworkLog.Warning($"{Name} receive unsubscribe message : {packetId}");
                }
                else
                {
                    UdpPacket packet = ReferencePool.Acquire(packetType) as UdpPacket;
                    packet.Deserialize(result.message, 0);
                    packet.endPoint = remoteEP;
                    AddReceivePacket(packet);
                }
            }
        }
    }
}