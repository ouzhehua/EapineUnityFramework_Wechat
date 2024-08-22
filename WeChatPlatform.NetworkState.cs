#if UNITY_WEBGL || UNITY_EDITOR
using VisionzFramework.Core.Platform;
using UnityEngine;
using WeChatWASM;
using System.Net;

namespace VisionzFramework.Runtime.WeChat
{
    public partial class WeChatPlatform : PlatformBase
    {
        public class WeChatNetworkState : NetworkState
        {
            public override NetworkReachability internetReachability
            {
                get
                {
                    return m_InternetReachability;
                }
            }

            public override IPAddress localIPAddress
            {
                get
                {
                    return m_LocalIPAddress;
                }
            }

            public override IPAddress localNetmask
            {
                get
                {
                    return m_LocalNetMask;
                }
            }

            public override IPAddress broadcastAddress
            {
                get
                {
                    return IPAddress.Broadcast;
                }
            }

            //当前网络状态
            protected NetworkReachability m_InternetReachability;
            //本机IP地址缓存
            protected IPAddress m_LocalIPAddress = IPAddress.None;
            //局域网子网掩码缓存
            protected IPAddress m_LocalNetMask = IPAddress.None;
            
            private GetNetworkTypeOption m_GetNetworkTypeOption;//获取网络状态参数
            private GetLocalIPAddressOption m_GetLocalIPAddressOption;//获取本地IP参数

            private const string c_NetworkType_Wifi = "wifi";
            private const string c_NetworkType_5G = "5g";
            private const string c_NetworkType_4G = "4g";
            private const string c_NetworkType_3G = "3g";
            private const string c_NetworkType_2G = "2g";

            public WeChatNetworkState()
            {
                m_GetNetworkTypeOption = new GetNetworkTypeOption();
                m_GetNetworkTypeOption.success = GetNetworkTypeSuccessCallback;
                m_GetNetworkTypeOption.fail = GeneralNetworkTypeFailCallback;
                WX.OnNetworkStatusChange(OnNetworkStatusChange);

                m_GetLocalIPAddressOption = new GetLocalIPAddressOption();
                m_GetLocalIPAddressOption.success = GetLocalIPAddressSuccessCallback;
                m_GetLocalIPAddressOption.fail = GetLocalIPAddressFailCallback;

                RefreshInternetReachability();
                RefreshLocalIPAddress();
            }

            //刷新网络状态
            public void RefreshInternetReachability()
            {
                WX.GetNetworkType(m_GetNetworkTypeOption);
            }

            private void GetNetworkTypeSuccessCallback(GetNetworkTypeSuccessCallbackResult result)
            {
                //result.hasSystemProxy
                //result.signalStrength
                //result.errMsg
                UpdateInternetReachability(result.networkType);

                Debug.Log($"GetNetworkType Success {result.networkType} {m_InternetReachability}");
            }

            private void GeneralNetworkTypeFailCallback(GeneralCallbackResult result)
            {
                m_InternetReachability = NetworkReachability.NotReachable;
                Debug.LogError("WX GetNetworkType Fail");
            }

            private void OnNetworkStatusChange(OnNetworkStatusChangeListenerResult result)
            {
                UpdateInternetReachability(result.networkType);
            }

            private void UpdateInternetReachability(string networkType)
            {
                switch (networkType)
                {
                    case c_NetworkType_Wifi:
                        m_InternetReachability = NetworkReachability.ReachableViaLocalAreaNetwork;
                        break;
                    case c_NetworkType_5G:
                    case c_NetworkType_4G:
                    case c_NetworkType_3G:
                    case c_NetworkType_2G:
                        m_InternetReachability = NetworkReachability.ReachableViaCarrierDataNetwork;
                        break;
                    default:
                        m_InternetReachability = NetworkReachability.NotReachable;
                        break;
                }

                //发个事件？
            }

            //刷新本地IP
            public void RefreshLocalIPAddress()
            {
                WX.GetLocalIPAddress(m_GetLocalIPAddressOption);
            }

            private void GetLocalIPAddressSuccessCallback(GetLocalIPAddressSuccessCallbackResult result)
            {
                bool flag = IPAddress.TryParse(result.localip, out m_LocalIPAddress);
                if (!flag)
                {
                    m_LocalIPAddress = IPAddress.None;
                    Debug.LogError($"GetLocalIPAddress Success IP:{result.localip}");
                }
                else
                {
                    Debug.Log($"GetLocalIPAddress Success IP:{result.localip}");
                }
                
                flag = IPAddress.TryParse(result.netmask, out m_LocalNetMask);
                if (!flag)
                {
                    m_LocalNetMask = IPAddress.None;
                    Debug.LogError($"GetLocalIPAddress Success NetMask:{result.netmask}");
                }
                else
                {
                    Debug.Log($"GetLocalIPAddress Success NetMask:{result.netmask}");
                }
            }

            private void GetLocalIPAddressFailCallback(GeneralCallbackResult result)
            {
                m_LocalIPAddress = IPAddress.None;
                m_LocalNetMask = IPAddress.None;
                Debug.LogError("GetLocalIPAddress Fail");
            }
        }
    }
}
#endif