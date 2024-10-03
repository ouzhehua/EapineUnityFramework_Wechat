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
        }

        private void InitFont()
        {
            try
            {
                WX.GetWXFont("https://shanmaiwangluo1.oss-cn-shenzhen.aliyuncs.com/download/SIMLI.TTF", LoadFontFinish);
                //WX.GetWXFont("", LoadFontFinish);
            }
            catch (Exception e)
            {
                Debug.LogError("InitFont Error");
                LoadFontFinish(null);
                throw;
            }
        }

        private bool m_FontFinish = false;
        private void LoadFontFinish(Font font)
        {
            if (font != null)
            {
                TMPro.TMP_RuntimeFontUGUI.AddFontAsset(TMPro.TMP_RuntimeFontSettings.GetNickNameByIndex(0), font);
            }
            else
            {
                Debug.LogError("LoadFontFinish Error");
            }
            
            m_FontFinish = true;
            CheckFinish();
        }

        private void CheckFinish()
        {
            if (m_FontFinish)
            {
                if (m_InitCallback != null)
                {
                    m_InitCallback(true);
                }

                m_InitCallback = null;
            }
        }
    }
}
#endif