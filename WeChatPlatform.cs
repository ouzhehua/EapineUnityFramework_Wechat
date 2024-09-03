#if UNITY_WEBGL || UNITY_EDITOR
using VisionzFramework.Core.Platform;
using System;
using UnityEngine;
using WeChatWASM;

namespace VisionzFramework.Runtime.WeChat
{
    public partial class WeChatPlatform : PlatformBase
    {
        private Action<bool> m_InitCallback;

        public override void InitPlatform(Action<bool> initCallback)
        {
            m_InitCallback = initCallback;
            WX.InitSDK(InitCallback);
        }

        //初始化WX SDK结束
        private void InitCallback(int code)
        {
            Debug.Log($"WeChatWASM SDK Init code:{code}");

            Screen = new WeChatScreen();
            NetworkState = new WeChatNetworkState();
            InitFont();

            if (m_InitCallback != null)
            {
                m_InitCallback(true);
            }

            m_InitCallback = null;
        }

        protected void InitFont()
        {
            WX.GetWXFont("https://shanmaiwangluo1.oss-cn-shenzhen.aliyuncs.com/download/SIMLI.TTF", LoadFontFinish);
        }

        private void LoadFontFinish(Font font)
        {
            TMPro.TMP_RuntimeFontUGUI.AddFontAsset(TMPro.TMP_RuntimeFontSettings.GetNickNameByIndex(0), font);
        }
    }
}
#endif