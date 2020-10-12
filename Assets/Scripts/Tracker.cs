using System;
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

    [Header("Tracker configuration settings")]
    //Tracker configuration file name.
    public string configFileEditor;

    public string configFileStandalone;
    public string configFileIOS;
    public string configFileAndroid;
    public string configFileOsx;


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

    public Vector3 position;
    public Vector3 rotation;
    public TrackStatus TrackStatus = 0; //TODO: Enum instead

    [Header("Camera settings")] public Material CameraViewMaterial;
    public float CameraFocus;
    public int Orientation;
    private int currentOrientation;
    public int isMirrored = 1;
    private int currentMirrored = 1;
    public int camDeviceId;
    private int AndroidCamDeviceId;
    private int currentCamDeviceId;
    public int defaultCameraWidth = -1;
    public int defaultCameraHeight = -1;
    private bool camInited;

    [Header("Texture settings")] public int ImageWidth = 800;
    public int ImageHeight = 600;
    public int TexWidth = 512;
    public int TexHeight = 512;
#if UNITY_ANDROID
    private TextureFormat TexFormat = TextureFormat.RGB24;
#else
    private TextureFormat TexFormat = TextureFormat.RGBA32;
#endif
    private Texture2D texture;
    private Color32[] texturePixels;
    private GCHandle texturePixelsHandle;

#if UNITY_ANDROID
    private AndroidJavaObject androidCameraActivity;
    private bool AppStarted;
    AndroidJavaClass unity;
#endif
    private Timer frameSkipTimer;

    #endregion

    private const string LicenseString = "dev.vlc";

    private void Awake()
    {
        frameSkipTimer = new Timer();
#if PLATFORM_ANDROID
        if (!Permission.HasUserAuthorizedPermission(Permission.Camera))
            Permission.RequestUserPermission(Permission.Camera);
        Unzip();
#endif

        string licenseFilePath = Application.streamingAssetsPath + "/" + "/Visage Tracker/";

        // Set license path depending on platform
        switch (Application.platform)
        {
            case RuntimePlatform.IPhonePlayer:
                licenseFilePath = "Data/Raw/Visage Tracker/";
                break;
            case RuntimePlatform.Android:
                licenseFilePath = Application.persistentDataPath + "/";
                break;
            case RuntimePlatform.OSXPlayer:
                licenseFilePath = Application.dataPath + "/Resources/Data/StreamingAssets/Visage Tracker/";
                break;
            case RuntimePlatform.OSXEditor:
                licenseFilePath = Application.dataPath + "/StreamingAssets/Visage Tracker/";
                break;
            case RuntimePlatform.WebGLPlayer:
                licenseFilePath = "";
                break;
            case RuntimePlatform.WindowsEditor:
                licenseFilePath = Application.streamingAssetsPath + "/Visage Tracker/";
                break;
        }

#if UNITY_STANDALONE_WIN
		//NOTE: licensing for Windows platform expects folder path exclusively
		VisageTrackerNative._initializeLicense(licenseFilePath);
#else
        //NOTE: platforms other than Windows expect absolute or relative path to the license file
        VisageTrackerNative._initializeLicense(licenseFilePath + LicenseString);
#endif
        Debug.Log("Awake end");
    }


    private void Start()
    {
        Screen.sleepTimeout = SleepTimeout.NeverSleep;
        Application.targetFrameRate = 60;

        Debug.Log("Starting tracker init");
        IsInit = InitializeTracker(ComputeConfigFilePath());
        Debug.Log("Done tracker init");

        camInited = OpenCamera(Orientation, camDeviceId, defaultCameraWidth, defaultCameraHeight, isMirrored);

        if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.OpenGLCore)
            Debug.Log("Notice: if graphics API is set to OpenGLCore, the texture might not get properly updated.");
        AutoConfigureCamera(); //TODO: reset instead ?
        Debug.Log("Start end");
    }

    private string ComputeConfigFilePath()
    {
        string configFilePath = Application.streamingAssetsPath + "/" + configFileStandalone;

        switch (Application.platform)
        {
            case RuntimePlatform.IPhonePlayer:
                configFilePath = "Data/Raw/Visage Tracker/" + configFileIOS;
                break;
            case RuntimePlatform.Android:
                configFilePath = Application.persistentDataPath + "/" + configFileAndroid;
                break;
            case RuntimePlatform.OSXPlayer:
                configFilePath = Application.dataPath + "/Resources/Data/StreamingAssets/Visage Tracker/" +
                                 configFileOsx;
                break;
            case RuntimePlatform.OSXEditor:
                configFilePath = Application.dataPath + "/StreamingAssets/Visage Tracker/" + configFileOsx;
                break;
            case RuntimePlatform.WindowsEditor:
                configFilePath = Application.streamingAssetsPath + "/" + configFileEditor;
                break;
        }

        return configFilePath;
    }


    private void Update()
    {
        frameSkipTimer.Update(Time.time);
        frameSkipTimer.SetDuration(FrameAnalysisDelay);

        if (!IsTrackerReady())
            return;

#if (UNITY_IPHONE || UNITY_ANDROID) && UNITY_EDITOR
        // tracking will not work if the target is set to Android or iOS while in editor
        // return;
#endif

        if (IsTrackerReady())
        {
#if UNITY_ANDROID
            if (VisageTrackerNative._frameChanged())
            {
                texture = null;
            }
#endif

            // Check if orientation or camera device changed
            if (currentOrientation != Orientation || currentCamDeviceId != camDeviceId || currentMirrored != isMirrored)
            {
                currentCamDeviceId = camDeviceId;
                currentOrientation = Orientation;
                currentMirrored = isMirrored;

                // Reopen camera with new parameters
                OpenCamera(currentOrientation, currentCamDeviceId, defaultCameraWidth, defaultCameraHeight,
                    currentMirrored);
                texture = null;
            }

            Profiler.BeginSample("grabFrame");
            // grab current frame and start face tracking
            VisageTrackerNative._grabFrame();
            Profiler.EndSample();

            if (frameSkipTimer.IsElapsed())
            {
                Profiler.BeginSample("track");
                VisageTrackerNative._track();
                Profiler.EndSample();
                frameSkipTimer.Reset();
            }

            int[] tStatus = new int[1];
            VisageTrackerNative._getTrackerStatus(tStatus);
            TrackStatus = (TrackStatus) tStatus[0];
            Quality = VisageTrackerNative._getTrackingQuality(0);
            FPS = VisageTrackerNative._getFrameRate();


            //After the track has been preformed on the new frame, the flags for the analysis and recognition are set to true

            // Set main camera field of view based on camera information
            // Get camera information from native
            float aspect = ImageWidth / (float) ImageHeight;
            float yRange = (ImageWidth > ImageHeight) ? 1.0f : 1.0f / aspect;
            Camera.main.fieldOfView = Mathf.Rad2Deg * 2.0f * Mathf.Atan(yRange / CameraFocus);
        }

        RefreshImage();

        if (TrackStatus == TrackStatus.OK)
        {
            //TODO: here
            UpdateControllableObjects();
        }
    }


    private bool IsTrackerReady()
    {
        return camInited;
    }

    private void OnDestroy()
    {
#if UNITY_ANDROID
        androidCameraActivity.Call("closeCamera");
#else
        camInited = !(VisageTrackerNative._closeCamera());
#endif
    }

#if UNITY_IPHONE
    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
        {
            camInited = !(VisageTrackerNative._closeCamera());
        }
        else
        {
            camInited = OpenCamera(Orientation, camDeviceId, defaultCameraWidth, defaultCameraHeight, isMirrored);
        }
    }
#endif

    /// <summary>
    /// Initialize tracker with maximum number of faces - MAX_FACES.
    /// Additionally, depending on a platform set an appropriate shader.
    /// </summary>
    /// <param name="config">Tracker configuration path and name.</param>
    private bool InitializeTracker(string config)
    {
        Debug.Log("Visage Tracker: Initializing tracker with config: '" + config + "'");

#if (UNITY_IPHONE || UNITY_ANDROID) && UNITY_EDITOR
        return false;
#endif

#if UNITY_ANDROID
        Shader shader = Shader.Find("Unlit/Texture");
        CameraViewMaterial.shader = shader;

        // initialize visage vision
        VisageTrackerNative._loadVisageVision();

        unity = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
        androidCameraActivity = unity.GetStatic<AndroidJavaObject>("currentActivity");
#elif UNITY_STANDALONE_WIN || UNITY_WEBGL
		Shader shader = Shader.Find("Custom/RGBATex");
		CameraViewMaterial.shader = shader;
#else
        Shader shader = Shader.Find("Custom/BGRATex");
        CameraViewMaterial.shader = shader;
#endif

        VisageTrackerNative._initTracker(config, 1);
        return true;
    }

    /// <summary>
    /// Update Unity texture with frame data from native camera.
    /// </summary>
    private void RefreshImage()
    {
        // Initialize texture
        if (texture == null && ImageWidth > 0)
        {
            TexWidth = Convert.ToInt32(Math.Pow(2.0, Math.Ceiling(Math.Log(ImageWidth) / Math.Log(2.0))));
            TexHeight = Convert.ToInt32(Math.Pow(2.0, Math.Ceiling(Math.Log(ImageHeight) / Math.Log(2.0))));
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

        if (texture != null && TrackStatus != (int) TrackStatus.OFF)
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


    public void AutoConfigureCamera()
    {
        Orientation = GetDeviceOrientation();
    }

    private int GetDeviceOrientation()
    {
        int devOrientation;

#if UNITY_ANDROID
        //Device orientation is obtained in AndroidCameraPlugin so we only need information about whether orientation is changed
        int oldWidth = ImageWidth;
        int oldHeight = ImageHeight;

        VisageTrackerNative._getCameraInfo(out CameraFocus, out ImageWidth, out ImageHeight);

        if ((oldWidth != ImageWidth || oldHeight != ImageHeight) && ImageWidth != 0 && ImageHeight != 0 &&
            oldWidth != 0 && oldHeight != 0)
            devOrientation = Orientation == 1 ? 0 : 1;
        else
            devOrientation = Orientation;
#else
        switch (Input.deviceOrientation)
        {
            case DeviceOrientation.Portrait:
                devOrientation = 0;
                break;
            case DeviceOrientation.PortraitUpsideDown:
                devOrientation = 2;
                break;
            case DeviceOrientation.LandscapeLeft:
                devOrientation = 3;
                break;
            case DeviceOrientation.LandscapeRight:
                devOrientation = 1;
                break;
            case DeviceOrientation.FaceUp:
            case DeviceOrientation.Unknown:
                devOrientation = Orientation;
                break;
            default:
                devOrientation = 0;
                break;
        }
#endif

        return devOrientation;
    }


    bool OpenCamera(int orientation, int cameraDeviceId, int width, int height, int isMirrored)
    {
#if UNITY_ANDROID
        if (cameraDeviceId == AndroidCamDeviceId && AppStarted)
            return false;

        AndroidCamDeviceId = cameraDeviceId;
        //camera needs to be opened on main thread
        androidCameraActivity.Call("runOnUiThread", new AndroidJavaRunnable(() =>
        {
            androidCameraActivity.Call("closeCamera");
            androidCameraActivity.Call("GrabFromCamera", width, height, camDeviceId);
        }));
        AppStarted = true;
        return true;
#elif UNITY_WEBGL
        VisageTrackerNative._openCamera(ImageWidth, ImageHeight, isMirrored, "OnSuccessCallbackCamera", "OnErrorCallbackCamera");
        return false;
#elif UNITY_STANDALONE_WIN
        VisageTrackerNative._openCamera(orientation, cameraDeviceId, width, height);
        return true;
#else
        VisageTrackerNative._openCamera(orientation, cameraDeviceId, width, height, isMirrored);
        return true;
#endif
    }

    private void UpdateControllableObjects()
    {
        //TODO: do in a single call to _get3DData
        float[] positionCoords = new float[3];
        float[] rotationCoords = new float[3];
        VisageTrackerNative._getHeadTranslation(positionCoords, 0);
        VisageTrackerNative._getHeadRotation(rotationCoords, 0);
        SetPositionAndRotation(positionCoords, rotationCoords);
    }

    private void SetPositionAndRotation(float[] positionCoords, float[] rotationCoords)
    {
        Assert.AreEqual(3, positionCoords.Length);
        Assert.AreEqual(3, rotationCoords.Length);
        position = new Vector3(
            -positionCoords[0],
            positionCoords[1],
            positionCoords[2]
        );
        int mirrorFactor = (Application.platform == RuntimePlatform.WindowsEditor ||
                            Application.platform == RuntimePlatform.WindowsPlayer) && isMirrored == 1
            ? -1
            : 1;
        rotation = new Vector3(
            -rotationCoords[0] * Mathf.Rad2Deg,
            mirrorFactor * -rotationCoords[1] * Mathf.Rad2Deg,
            mirrorFactor * rotationCoords[2] * Mathf.Rad2Deg
        );
    }

    //TODO: Android specific below: move to another file ?
#if UNITY_ANDROID
    private static void Unzip()
    {
        string[] pathsNeeded =
        {
            "candide3.fdp",
            "candide3.wfm",
            "jk_300.fdp",
            "jk_300.wfm",
            "Head Tracker.cfg",
            "Facial Features Tracker - High.cfg",
            "NeuralNet.cfg",
            "bdtsdata/FF/ff.dat",
            "bdtsdata/FF/vnn/ff.tflite",
            "bdtsdata/LBF/lv",
            "bdtsdata/LBF/vfadata/ad/ae.bin",
            "bdtsdata/LBF/vfadata/ad/ae.tflite",
            "bdtsdata/LBF/vfadata/ed/ed0.lbf",
            "bdtsdata/LBF/vfadata/ed/ed1.lbf",
            "bdtsdata/LBF/vfadata/ed/ed2.lbf",
            "bdtsdata/LBF/vfadata/ed/ed3.lbf",
            "bdtsdata/LBF/vfadata/ed/ed4.lbf",
            "bdtsdata/LBF/vfadata/ed/ed5.lbf",
            "bdtsdata/LBF/vfadata/ed/ed6.lbf",
            "bdtsdata/LBF/vfadata/gd/gd.lbf",
            "bdtsdata/NN/fa.lbf",
            "bdtsdata/NN/efa.lbf",
            "bdtsdata/NN/fc.lbf",
            "bdtsdata/NN/efc.lbf",
            "bdtsdata/NN/fr.tflite",
            "bdtsdata/NN/pr.bin",
            "bdtsdata/NN/pr.tflite",
            "bdtsdata/NN/is.bin",
            "bdtsdata/NN/model.bin",
            "bdtsdata/NN/model.tflite",
            "bdtsdata/NN/vnn/init_shape.bin",
            "bdtsdata/NN/vnn/landmarks.bin",
            "bdtsdata/NN/vnn/mean_image.bin",
            "bdtsdata/NN/vnn/std_dev_image.bin",
            "bdtsdata/NN/vnn/d1qy.tflite",
            "bdtsdata/NN/vnn/d2.tflite",
            "dev.vlc"
        };
        const string localDataFolder = "Visage Tracker";

        string outputDir = Application.persistentDataPath;

        if (!Directory.Exists(outputDir))
        {
            Directory.CreateDirectory(outputDir);
        }

        foreach (string filename in pathsNeeded)
        {
            WWW unpacker =
                new WWW("jar:file://" + Application.dataPath + "!/assets/" + localDataFolder + "/" + filename);

            while (!unpacker.isDone)
            {
            }

            if (!string.IsNullOrEmpty(unpacker.error))
            {
                continue;
            }

            if (filename.Contains("/"))
            {
                string[] split = filename.Split('/');
                string name = "";
                string folder = "";
                string curDir = outputDir;

                for (int i = 0; i < split.Length; i++)
                {
                    if (i == split.Length - 1)
                    {
                        name = split[i];
                    }
                    else
                    {
                        folder = split[i];
                        curDir = curDir + "/" + folder;
                    }
                }

                if (!Directory.Exists(curDir))
                {
                    Directory.CreateDirectory(curDir);
                }

                File.WriteAllBytes("/" + curDir + "/" + name, unpacker.bytes);
            }
            else
            {
                File.WriteAllBytes("/" + outputDir + "/" + filename, unpacker.bytes);
            }
        }
    }
#endif
    public float FaceDistance()
    {
        return position.magnitude + Offset;
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
        AutoConfigureCamera();
        Offset = 0;
        Smoothing = 0.05f;
        frameSkipTimer.SetDuration(0);
    }
}
