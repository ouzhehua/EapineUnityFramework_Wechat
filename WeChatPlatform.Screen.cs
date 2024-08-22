#if UNITY_WEBGL || UNITY_EDITOR
using VisionzFramework.Core.Platform;
using UnityEngine;
using WeChatWASM;

namespace VisionzFramework.Runtime.WeChat
{
    public partial class WeChatPlatform : PlatformBase
    {
        public class WeChatScreen : IScreen
        {
            public int width
            {
                get
                {
                    RefreshScreenSize();
                    return (int)m_CacheWindowInfo.screenWidth;
                }
            }

            public int height
            {
                get
                {
                    RefreshScreenSize();
                    return (int)m_CacheWindowInfo.screenHeight;
                }
            }

            public float dpi
            {
                get
                {
                    Debug.LogError("WeChatPlatform not support get dpi");
                    return UnityEngine.Screen.dpi;
                }
            }

            public Rect safeArea
            {
                get
                {
                    //safeArea.bottom;//安全区域右下角纵坐标
                    //safeArea.height;//安全区域的高度，单位逻辑像素
                    //safeArea.left;//安全区域左上角横坐标
                    //safeArea.right;//安全区域右下角横坐标
                    //safeArea.top;//安全区域左上角纵坐标
                    //safeArea.width;//安全区域的宽度，单位逻辑像素
                    return new Rect((float)m_CacheWindowInfo.safeArea.left, (float)(m_CacheWindowInfo.screenHeight - m_CacheWindowInfo.safeArea.bottom), (float)m_CacheWindowInfo.safeArea.width, (float)m_CacheWindowInfo.safeArea.height);
                }
            }

            public ScreenOrientation orientation
            {
                get
                {
                    SystemSetting systemSetting = WX.GetSystemSetting();
                    return systemSetting.deviceOrientation == "landscape" ? ScreenOrientation.LandscapeLeft : ScreenOrientation.Portrait;
                }
                set
                {
                    m_SetDeviceOrientationOption.value = (value == ScreenOrientation.LandscapeLeft || value == ScreenOrientation.LandscapeRight) ? "landscape" : "portrait";
                    WX.SetDeviceOrientation(m_SetDeviceOrientationOption);
                }
            }

            private WindowInfo m_CacheWindowInfo;
            private SetDeviceOrientationOption m_SetDeviceOrientationOption = new SetDeviceOrientationOption();

            public void RefreshScreenSize()
            {
                m_CacheWindowInfo = WX.GetWindowInfo();
            }
        }
    }
}
#endif