using System;
using System.IO;
using UnityEngine;
using UnityEngine.Profiling;
#if PLATFORM_ANDROID
using UnityEngine.Android;

#endif

/// <summary>
/// Class to centralize calls to visage tracker SDK
/// Platform independent
/// Doesn't fail; explicitly throws error
/// </summary>
public static class VisageTrackerApi
{
    private const string ConfigFolder = "Visage Tracker";
    private const string ConfigFileName = "Head Tracker.cfg";
    private const int FaceCount = 1;
    private const int FaceIndex = 0;
    private const string LicenseFileName = "license.vlc";

    #region API

    public static bool IsInit { get; private set; }
    public static CameraInfo LastCameraInfo { get; set; }
    public static HeadInfo LastHeadInfo { get; set; }
    public static TrackerStatus Status { get; set; }

    public static bool IsMirrored => IsMirroredPlatform(Application.platform);

    public static void Init()
    {
        IsInit = AskCameraPermission() && ActivateLicense() && InitFromConfigFile() && OpenCamera();
    }

    public static void UpdateCameraInfo()
    {
        LastCameraInfo = GetCameraInfo();
    }

    /// <returns>returns specific shader with correct pixel ordering</returns>
    /// TODO: here
    public static Shader GetShaderForPlatform()
    {
        // ReSharper disable once SwitchStatementHandlesSomeKnownEnumValuesWithDefault
        switch (Application.platform)
        {
            case RuntimePlatform.Android:
                return Shader.Find("Unlit/Texture");
            case RuntimePlatform.WindowsPlayer:
            case RuntimePlatform.WebGLPlayer:
                return Shader.Find("Custom/RGBATex");
            default:
                return Shader.Find("Custom/BGRATex");
        }
    }

    public static void GrabFrame()
    {
        Profiler.BeginSample("grabFrame");
        VisageTrackerNative._grabFrame();
        Profiler.EndSample();
    }

    public static void Track()
    {
        Profiler.BeginSample("track");
        VisageTrackerNative._track();
        Profiler.EndSample();

        Status = GetTrackerStatus();
        LastHeadInfo = GetHeadInfo();
    }

    public struct HeadInfo
    {
        public Vector3 Position;
        public Vector3 Rotation;
        public bool LeftEyeOpening; //[0-1] Range. 0 is fully closed
        public bool RightEyeOpening; //[0-1] Range. 0 is fully closed
        public float IrisRadiusLeft { get; set; }
        public float IrisRadiusRight { get; set; }
    }

    public struct TrackerStatus
    {
        public float Quality;
        public TrackStatus TrackingStatus; //TODO: enum
        public float FrameRate;
    }

    public struct CameraInfo
    {
        public double FocalLenght;
        public Vector2Int ImageSize;
    }

    public enum TrackStatus
    {
        Off = 0,
        Ok = 1,
        Recovering = 2,
        Init = 3
    }

    public static void Release()
    {
        if (Application.isEditor) return;
#if UNITY_ANDROID
        androidCameraActivity.Call("closeCamera");
#else
        VisageTrackerNative._closeCamera();
#endif
    }

    #endregion

    private static bool IsMirroredPlatform(RuntimePlatform platform)
    {
        return platform == RuntimePlatform.WindowsEditor
               || platform == RuntimePlatform.IPhonePlayer
               || platform == RuntimePlatform.OSXPlayer;
    }

    private static HeadInfo GetHeadInfo()
    {
        float[] positionCoords = new float[3];
        float[] rotationCoords = new float[3];
        float[] eyesClosure = new float[2];
        float[] irisRadius = new float[2];
        VisageTrackerNative._getHeadTranslation(positionCoords, FaceIndex);
        VisageTrackerNative._getHeadRotation(rotationCoords, FaceIndex);
        VisageTrackerNative._getEyeClosure(eyesClosure, FaceIndex);
        VisageTrackerNative._getIrisRadius(irisRadius, FaceCount);

        int mirrorFactor = IsMirrored ? -1 : 1;
        return new HeadInfo
        {
            Position = new Vector3(
                -positionCoords[0] * mirrorFactor,
                positionCoords[1],
                positionCoords[2]
            ),
            Rotation = new Vector3(
                -rotationCoords[0] * Mathf.Rad2Deg,
                -rotationCoords[1] * Mathf.Rad2Deg * mirrorFactor,
                rotationCoords[2] * Mathf.Rad2Deg * mirrorFactor
            ),
            LeftEyeOpening = eyesClosure[0] >= 1,
            RightEyeOpening = eyesClosure[1] >= 1,
            IrisRadiusLeft = irisRadius[0],
            IrisRadiusRight = irisRadius[1]
        };
    }

    private static TrackerStatus GetTrackerStatus()
    {
        if (Application.isEditor) return new TrackerStatus();
        int[] tStatus = new int[1];
        VisageTrackerNative._getTrackerStatus(tStatus);
        return new TrackerStatus
        {
            Quality = VisageTrackerNative._getTrackingQuality(FaceIndex),
            FrameRate = VisageTrackerNative._getFrameRate(),
            TrackingStatus = (TrackStatus) tStatus[0]
        };
    }


    private static CameraInfo GetCameraInfo()
    {
        if (Application.isEditor) return new CameraInfo();
        VisageTrackerNative._getCameraInfo(out float focus, out int w, out int h);
        return new CameraInfo
        {
            FocalLenght = focus,
            ImageSize = new Vector2Int(w, h)
        };
    }

    private static bool AskCameraPermission()
    {
        try
        {
#if PLATFORM_ANDROID //Preprocessor macro because `Permission` class is specific to Android
            const string camera = Permission.Camera;
            if (!Permission.HasUserAuthorizedPermission(camera)) Permission.RequestUserPermission(camera);
#endif
            if (Application.platform == RuntimePlatform.Android) Unzip();
            return true;
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            return false;
        }
    }

    private static bool ActivateLicense()
    {
        if (Application.isEditor) return true;
        try
        {
            string computeLicensePath = ComputeLicensePath();
            Debug.Log($"Reading licence at : {computeLicensePath}");
            VisageTrackerNative._initializeLicense(computeLicensePath);
            return true;
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            return false;
        }
    }

#if UNITY_ANDROID
    private static AndroidJavaObject androidCameraActivity;
#endif

    private static bool InitFromConfigFile()
    {
        if (Application.isEditor) return false;
        try
        {
#if UNITY_ANDROID
            VisageTrackerNative._loadVisageVision();
            androidCameraActivity =
                new AndroidJavaClass("com.unity3d.player.UnityPlayer")
                    .GetStatic<AndroidJavaObject>("currentActivity");
#endif

            VisageTrackerNative._initTracker(ComputeConfigFilePath(), FaceCount);
            return true;
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            return false;
        }
    }

    //Had to be public because iOS must explictely reopen the camera on resuming 
    //Will be removed once we access pixels otherwise
    public static bool OpenCamera()
    {
        const int cameraId = 0;
        const int width = 800;
        const int height = 600;
        try
        {
#if UNITY_ANDROID
            //camera needs to be opened on main thread
            androidCameraActivity.Call("runOnUiThread", new AndroidJavaRunnable(() =>
            {
                androidCameraActivity.Call("closeCamera");
                androidCameraActivity.Call("GrabFromCamera", width, height, cameraId);
            }));
            return true;
#elif UNITY_STANDALONE_WIN
            VisageTrackerNative._openCamera(0, cameraId, width, height);
            return true;
#else
            VisageTrackerNative._openCamera(0, cameraId, width, height, IsMirrored ? 1 : 0);
            return true;
#endif
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            return false;
        }
    }

    private static string ComputeLicensePath()
    {
        // ReSharper disable once SwitchStatementHandlesSomeKnownEnumValuesWithDefault
        switch (Application.platform)
        {
            case RuntimePlatform.IPhonePlayer:
                return $"Data/Raw/{ConfigFolder}/{LicenseFileName}";
            case RuntimePlatform.Android:
                return $"{Application.persistentDataPath}/{LicenseFileName}";
            case RuntimePlatform.OSXPlayer:
                return $"{Application.dataPath}/Resources/Data/StreamingAssets/{ConfigFolder}/{LicenseFileName}";
            case RuntimePlatform.OSXEditor:
                return $"{Application.dataPath}/StreamingAssets/{ConfigFolder}/{LicenseFileName}";
            case RuntimePlatform.WebGLPlayer:
                return "" + LicenseFileName;
            case RuntimePlatform.WindowsEditor:
                return $"{Application.streamingAssetsPath}/{ConfigFolder}/";
            case RuntimePlatform.WindowsPlayer: //Windows needs folder path, not the file path
                return $"{Application.streamingAssetsPath}//{ConfigFolder}/";
            default:
                return $"{Application.streamingAssetsPath}//{ConfigFolder}/{LicenseFileName}";
        }
    }


    private static string ComputeConfigFilePath()
    {
        switch (Application.platform)
        {
            case RuntimePlatform.IPhonePlayer:
                return $"Data/Raw/{ConfigFolder}/{ConfigFileName}";
            case RuntimePlatform.Android:
                return $"{Application.persistentDataPath}/{ConfigFileName}";
            case RuntimePlatform.OSXPlayer:
                return $"{Application.dataPath}/Resources/Data/StreamingAssets/{ConfigFolder}/{ConfigFileName}";
            case RuntimePlatform.OSXEditor:
                return $"{Application.dataPath}/StreamingAssets/{ConfigFolder}/{ConfigFileName}";
            case RuntimePlatform.WindowsEditor:
                return $"{Application.streamingAssetsPath}/{ConfigFolder}/{ConfigFileName}";
            default:
                return $"{Application.streamingAssetsPath}/{ConfigFolder}/{ConfigFileName}";
        }
    }

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
            LicenseFileName
        };
        const string localDataFolder = "Visage Tracker";

        string outputDir = Application.persistentDataPath;

        if (!Directory.Exists(outputDir))
        {
            Directory.CreateDirectory(outputDir);
        }

        foreach (string filename in pathsNeeded)
        {
#pragma warning disable 618
            WWW unpacker =
                new WWW("jar:file://" + Application.dataPath + "!/assets/" + localDataFolder + "/" + filename);
#pragma warning restore 618

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
                string curDir = outputDir;

                for (int i = 0; i < split.Length; i++)
                {
                    if (i == split.Length - 1)
                    {
                        name = split[i];
                    }
                    else
                    {
                        string folder = split[i];
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
}
