using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using DefaultNamespace;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using UnityEngine.UI;
#if PLATFORM_ANDROID && UNITY_2018_3_OR_NEWER
using UnityEngine.Android;

#endif


/** Class that implements the behavior of tracking application.
 *
 * This is the core class that shows how to use visage|SDK capabilities in Unity. It connects with visage|SDK through calls to
 * native methods that are implemented in VisageTrackerUnityPlugin.
 * It uses tracking data to transform objects that are attached to it in ControllableObjects list.
 */
public partial class Tracker : MonoBehaviour
{
    #region Properties

    [Header("Tracker configuration settings")]
    //Tracker configuration file name.
    public string ConfigFileEditor;

    public string ConfigFileStandalone;
    public string ConfigFileIOS;
    public string ConfigFileAndroid;
    public string ConfigFileOSX;


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

    [Header("Tracking settings")]
    // Mesh information
    private const int MaxVertices = 1024;

    private const int MaxTriangles = 2048;

    private int VertexNumber;
    private Vector2[] TexCoords = { };
    private Vector3[] Vertices = new Vector3[MaxVertices];
    private int[] Triangles = { };
    private float[] vertices = new float[MaxVertices * 3];
    private int[] triangles = new int[MaxTriangles * 3];
    private float[] texCoords = new float[MaxVertices * 2];
    private MeshFilter meshFilter;
    private Vector2[] modelTexCoords;

    [Header("Tiger texture mapping file")] public TextAsset TexCoordinatesFile;

    [Header("Tracker output data info")] public Vector3 Translation = new Vector3();
    public Vector3 Rotation = new Vector3();
    public TrackStatus TrackStatus = 0; //TODO: Enum instead
    private float[] translation = new float[3];
    private float[] rotation = new float[3];

    [Header("Camera settings")] public Material CameraViewMaterial;
    public Shader CameraViewShaderRGBA;
    public Shader CameraViewShaderBGRA;
    public Shader CameraViewShaderUnlit;
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

    [Header("GUI button settings")] public Button trackingButton;
    public Button portrait;
    public Button portraitUpside;
    public Button landscapeRight;
    public Button landscapeLeft;

    private bool texCoordsStaticLoaded;

#if UNITY_ANDROID
    private AndroidJavaObject androidCameraActivity;
    private bool AppStarted;
    AndroidJavaClass unity;
    private Timer frameSkipTimer;
#endif

    #endregion

    private const string LicenseString = "dev.vlc";

    private void Awake()
    {
        frameSkipTimer = new Timer();
        Smoothing = 0.05f;
#if PLATFORM_ANDROID && UNITY_2018_3_OR_NEWER
        if (!Permission.HasUserAuthorizedPermission(Permission.Camera))
            Permission.RequestUserPermission(Permission.Camera);
#endif
#if UNITY_ANDROID
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
    }


    private void Start()
    {
        Screen.sleepTimeout = SleepTimeout.NeverSleep;
        Application.targetFrameRate = 60;
        // Create an empty mesh and load tiger texture coordinates
        meshFilter = GetComponent<MeshFilter>();
        meshFilter.mesh = new Mesh();

        // Set configuration file path and name depending on a platform
        string configFilePath = Application.streamingAssetsPath + "/" + ConfigFileStandalone;

        switch (Application.platform)
        {
            case RuntimePlatform.IPhonePlayer:
                configFilePath = "Data/Raw/Visage Tracker/" + ConfigFileIOS;
                break;
            case RuntimePlatform.Android:
                configFilePath = Application.persistentDataPath + "/" + ConfigFileAndroid;
                break;
            case RuntimePlatform.OSXPlayer:
                configFilePath = Application.dataPath + "/Resources/Data/StreamingAssets/Visage Tracker/" +
                                 ConfigFileOSX;
                break;
            case RuntimePlatform.OSXEditor:
                configFilePath = Application.dataPath + "/StreamingAssets/Visage Tracker/" + ConfigFileOSX;
                break;
            case RuntimePlatform.WindowsEditor:
                configFilePath = Application.streamingAssetsPath + "/" + ConfigFileEditor;
                break;
        }

        // Initialize tracker with configuration and MAX_FACES
        IsInit = InitializeTracker(configFilePath);


        // Open camera in native code
        camInited = OpenCamera(Orientation, camDeviceId, defaultCameraWidth, defaultCameraHeight, isMirrored);

        if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.OpenGLCore)
            Debug.Log("Notice: if graphics API is set to OpenGLCore, the texture might not get properly updated.");
        // Get current device orientation
        AutoConfigureCamera(); //TODO: reset instead ?
    }


    private void Update()
    {
        //signals analysis and recognition to stop if camera or tracker are not initialized and until new frame and tracking data are obtained
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
            if (!texCoordsStaticLoaded)
            {
                texCoordsStaticLoaded = GetTextureCoordinates(out modelTexCoords);
            }
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
	void OnApplicationPause(bool pauseStatus) {
		if(pauseStatus){
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

    /// <summary>
    /// Get current device orientation.
    /// </summary>
    /// <returns>Returns an integer:
    /// <list type="bullet">
    /// <item><term>0 : DeviceOrientation.Portrait</term></item>
    /// <item><term>1 : DeviceOrientation.LandscapeRight</term></item>
    /// <item><term>2 : DeviceOrientation.PortraitUpsideDown</term></item>
    /// <item><term>3 : DeviceOrientation.LandscapeLeft</term></item>
    /// </list>
    /// </returns>
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
		if (Input.deviceOrientation == DeviceOrientation.Portrait)
			devOrientation = 0;
		else if (Input.deviceOrientation == DeviceOrientation.PortraitUpsideDown)
			devOrientation = 2;
		else if (Input.deviceOrientation == DeviceOrientation.LandscapeLeft)
			devOrientation = 3;
		else if (Input.deviceOrientation == DeviceOrientation.LandscapeRight)
			devOrientation = 1;
		else if (Input.deviceOrientation == DeviceOrientation.FaceUp)
			devOrientation = Orientation;
        else if (Input.deviceOrientation == DeviceOrientation.Unknown)
            devOrientation = Orientation;
        else
			devOrientation = 0;
#endif

        return devOrientation;
    }


    /// <summary>
    /// Open camera from native code.
    /// </summary>
    /// <param name="orientation">Current device orientation:
    /// <list type="bullet">
    /// <item><term>0 : DeviceOrientation.Portrait</term></item>
    /// <item><term>1 : DeviceOrientation.LandscapeRight</term></item>
    /// <item><term>2 : DeviceOrientation.PortraitUpsideDown</term></item>
    /// <item><term>3 : DeviceOrientation.LandscapeLeft</term></item>
    /// </list>
    /// </param>
    /// <param name="camDeviceId">ID of the camera device.</param>
    /// <param name="width">Desired width in pixels (pass -1 for default 800).</param>
    /// <param name="height">Desired width in pixels (pass -1 for default 600).</param>
    /// <param name="isMirrored">true if frame is to be mirrored, false otherwise.</param>
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

    /// <summary>
    /// Apply data from the tracker to controllable objects (glasses, tiger mesh).
    /// </summary>
    private void UpdateControllableObjects()
    {
        VisageTrackerNative._getFaceModelTriangleCount();
        VertexNumber = VisageTrackerNative._getFaceModelVertexCount();
        meshFilter.mesh.Clear();

        // update translation and rotation
        //TODO: do in a single call to _get3DData
        VisageTrackerNative._getHeadTranslation(translation, 0);
        VisageTrackerNative._getHeadRotation(rotation, 0);
        // an example for points 2.1, 8.3 and 8.4
        const int N = 2;
        int[] groups = new int[N] {3, 3};
        int[] indices = new int[N] {5, 6};
        float[] positions3D = new float[3 * N];
        int[] defined = new int[N];
        int[] detected = new int[N];
        float[] quality = new float[N];
        VisageTrackerNative._getFeaturePoints3D(N, groups, indices, positions3D, defined, detected, quality, 0);

        //TODO: Function
        Translation.x = translation[0];
        Translation.y = translation[1];
        Translation.z = translation[2];

        Rotation.x = rotation[0];

        if ((Application.platform == RuntimePlatform.WindowsEditor ||
             Application.platform == RuntimePlatform.WindowsPlayer) && isMirrored == 1)
        {
            Rotation.y = -rotation[1];
            Rotation.z = -rotation[2];
        }
        else
        {
            Rotation.y = rotation[1];
            Rotation.z = rotation[2];
        }

        Transform3DData();
        //Face effect tiger
        {
            // VisageTrackerNative._getFaceModelVertices(vertices, 0);
            // VisageTrackerNative._getFaceModelTriangles(triangles, 0);
        }
    }

    /// <summary>
    /// Helper function for transforming data obtained from tracker
    /// </summary>
    private void Transform3DData()
    {
        //TODO: replace by Mathf.PI ?
        Translation.x *= -1;
        Rotation.x = 180.0f * Rotation.x / 3.14f;
        Rotation.y += 3.14f;
        Rotation.y = 180.0f * -Rotation.y / 3.14f;
        Rotation.z = 180.0f * -Rotation.z / 3.14f;
    }

    /// <summary>
    /// Loads static texture coordinates from the plugin.
    /// </summary>
    /// <returns>Returns true on successful load, false otherwise.</returns>
    bool GetTextureCoordinates(out Vector2[] texCoords)
    {
        int texCoordsNumber;
        float[] buffer = new float[1024];
        VisageTrackerNative._getTexCoordsStatic(buffer, out texCoordsNumber);

        texCoords = new Vector2[texCoordsNumber / 2];
        for (int i = 0; i < texCoordsNumber / 2; i++)
        {
            texCoords[i] = new Vector2(buffer[i * 2], buffer[i * 2 + 1]);
        }

        return texCoordsNumber > 0;
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
        return Translation.magnitude + Offset;
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
    }
}
