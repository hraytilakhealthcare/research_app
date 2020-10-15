using System;
using System.Collections;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
#if PLATFORM_ANDROID && UNITY_2018_3_OR_NEWER
using System.IO;
using UnityEngine.Android;

#endif


public class Tracker : MonoBehaviour
{
    #region Properties

    public float FrameAnalysisDelay { get; set; }
    public float Smoothing { get; private set; }
    public bool IsInit { get; private set; }
    public float Quality { get; private set; }
    public float FPS { get; private set; }
    private float Offset { get; set; }

    public float IPD
    {
        get => VisageTrackerNative._getIPD();
        set => VisageTrackerNative._setIPD(value);
    }

    public Material CameraViewMaterial;
    public float CameraFocus;
    public int Orientation;

    [Header("Texture settings")] public int TexWidth = 512;
    public int TexHeight = 512;
#if UNITY_ANDROID
    private TextureFormat TexFormat = TextureFormat.RGB24;
#else
    private TextureFormat TexFormat = TextureFormat.RGBA32;
#endif
    private Texture2D texture;
    private Color32[] texturePixels;
    private GCHandle texturePixelsHandle;

    private Timer frameSkipTimer;

    #endregion


    private IEnumerator Start()
    {
        frameSkipTimer = new Timer();
        Screen.sleepTimeout = SleepTimeout.NeverSleep;
        Application.targetFrameRate = 60;
        yield return new WaitForSeconds(10);
        IsInit = InitializeTracker();
        if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.OpenGLCore)
            Debug.Log("Notice: if graphics API is set to OpenGLCore, the texture might not get properly updated.");
    }

    private void Update()
    {
        if (!VisageTrackerApi.IsInit) return;

        frameSkipTimer.Update(Time.time);
        frameSkipTimer.SetDuration(FrameAnalysisDelay);

        VisageTrackerApi.GrabFrame();
        if (frameSkipTimer.IsElapsed())
        {
            VisageTrackerApi.Track();
            frameSkipTimer.Reset();
        }

        VisageTrackerApi.TrackerStatus status = VisageTrackerApi.Status;
        VisageTrackerApi.CameraInfo info = VisageTrackerApi.LastCameraInfo;

        Quality = status.Quality;
        FPS = status.FrameRate;
        UpdateCameraFov(info);

        RefreshImage();
    }

    private void UpdateCameraFov(VisageTrackerApi.CameraInfo info)
    {
        float aspect = info.ImageSize.x / (float) info.ImageSize.y;
        float yRange = info.ImageSize.x > info.ImageSize.y ? 1.0f : 1.0f / aspect;
        float fov = Camera.main.fieldOfView = Mathf.Rad2Deg * 2.0f * Mathf.Atan((float) (yRange / info.FocalLenght));
        Debug.Log($"Setting fov to {fov}");
    }

    private void OnDestroy()
    {
        VisageTrackerApi.Release();
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (Application.platform != RuntimePlatform.OSXPlayer) return;
        if (pauseStatus)
        {
            VisageTrackerApi.Release();
        }
        else
        {
            VisageTrackerApi.OpenCamera();
        }
    }

    private bool InitializeTracker()
    {
#if UNITY_ANDROID
        CameraViewMaterial.shader = Shader.Find("Unlit/Texture");
#elif UNITY_STANDALONE_WIN || UNITY_WEBGL
		CameraViewMaterial.shader = Shader.Find("Custom/RGBATex");
#else
        CameraViewMaterial.shader = Shader.Find("Custom/BGRATex");
#endif
        texture = null; //To force-rebuilding it
        VisageTrackerApi.Init();
        return VisageTrackerApi.IsInit;
    }

    private void RefreshImage()
    {
        // Initialize texture
        Vector2Int imageSize = VisageTrackerApi.LastCameraInfo.ImageSize;
        if (texture == null && imageSize.x > 0)
        {
            TexWidth = Convert.ToInt32(Math.Pow(2.0, Math.Ceiling(Math.Log(imageSize.x) / Math.Log(2.0))));
            TexHeight = Convert.ToInt32(Math.Pow(2.0, Math.Ceiling(Math.Log(imageSize.y) / Math.Log(2.0))));
            texture = new Texture2D(TexWidth, TexHeight, TexFormat, false);

            Color32[] cols = texture.GetPixels32();
            for (int i = 0; i < cols.Length; i++)
                cols[i] = Color.black;

            texture.SetPixels32(cols);
            texture.Apply(false);

            CameraViewMaterial.SetTexture("_MainTex", texture);

#if UNITY_STANDALONE_WIN
			// "pin" the pixel array in memory, so we can pass direct pointer to it's data to the plugin,
			// without costly marshaling of array of structures.
			texturePixels = ((Texture2D)texture).GetPixels32(0);
			texturePixelsHandle = GCHandle.Alloc(texturePixels, GCHandleType.Pinned);
#endif
        }

        if (texture != null && VisageTrackerApi.Status.TrackingStatus != VisageTrackerApi.TrackStatus.Off)
        {
#if UNITY_STANDALONE_WIN
			// send memory address of textures' pixel data to VisageTrackerUnityPlugin
			VisageTrackerNative._setFrameData(texturePixelsHandle.AddrOfPinnedObject());
			((Texture2D)texture).SetPixels32(texturePixels, 0);
			((Texture2D)texture).Apply();
#elif UNITY_IPHONE || UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX || UNITY_ANDROID
            if (SystemInfo.graphicsDeviceVersion.StartsWith("Metal"))
                VisageTrackerNative._bindTextureMetal(texture.GetNativeTexturePtr());
            else
                VisageTrackerNative._bindTexture((int) texture.GetNativeTexturePtr());
#endif
        }
    }

    public float this[TrackerProperty property]
    {
        get
        {
            switch (property)
            {
                case TrackerProperty.IPD:
                    return IPD;
                case TrackerProperty.DistanceOffset:
                    return Offset;
                case TrackerProperty.FocalLenght:
                    return CameraFocus;
                case TrackerProperty.Smoothing:
                    return Smoothing;
                case TrackerProperty.FrameAnalysisDelay:
                    return FrameAnalysisDelay;
                default:
                    throw new ArgumentOutOfRangeException(nameof(property), property, null);
            }
        }
        set
        {
            switch (property)
            {
                case TrackerProperty.IPD:
                    IPD = value;
                    break;
                case TrackerProperty.DistanceOffset:
                    Offset = value;
                    break;
                case TrackerProperty.FocalLenght:
                    CameraFocus = value;
                    break;
                case TrackerProperty.Smoothing:
                    Smoothing = value;
                    break;
                case TrackerProperty.FrameAnalysisDelay:
                    FrameAnalysisDelay = value;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(property), property, null);
            }
        }
    }


    public void ResetValues()
    {
        Offset = 0;
        Smoothing = 0.05f;
        frameSkipTimer.SetDuration(0);
        texture = null;
    }
}
