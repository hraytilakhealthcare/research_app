using System;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;

#if PLATFORM_ANDROID && UNITY_2018_3_OR_NEWER
using UnityEngine.Android;
#endif

using System.IO;
using System.Linq;

/** Enum used for Face effects - tiger or glasses model
 */
public enum FaceEffect
{
	Glasses = 0,
	Tiger = 1
}

/** Class that implements the behavior of tracking application.
 * 
 * This is the core class that shows how to use visage|SDK capabilities in Unity. It connects with visage|SDK through calls to 
 * native methods that are implemented in VisageTrackerUnityPlugin.
 * It uses tracking data to transform objects that are attached to it in ControllableObjects list.
 */
public class Tracker : MonoBehaviour
{
	#region Properties

	[Header("Tracker configuration settings")]
	//Tracker configuration file name.
	public string ConfigFileEditor;
	public string ConfigFileStandalone;
	public string ConfigFileIOS;
	public string ConfigFileAndroid;
	public string ConfigFileOSX;
    public string ConfigFileWebGL;


	[Header("Tracking settings")]
#if UNITY_WEBGL
    public const int MAX_FACES = 1;
#else
    public const int MAX_FACES = 4;
#endif

    private bool trackerInited = false;

	[Header("Controllable object info")]
	public Transform[] ControllableObjectsGlasses;
	public Transform[] ControllableObjectsTiger;
	Vector3[] startingPositionsGlasses;
	Vector3[] startingRotationsGlasses;
	Vector3[] startingPositionsTiger;
	Vector3[] startingRotationsTiger;

    // Mesh information
    private const int MaxVertices = 1024;
    private const int MaxTriangles = 2048;

    private int VertexNumber = 0;
    private int TriangleNumber = 0;
    private Vector2[] TexCoords = { };
    private Vector3[][] Vertices = new Vector3[MAX_FACES][];
    private int[] Triangles = { };
	private float[] vertices = new float[MaxVertices * 3];
	private int[] triangles = new int[MaxTriangles * 3];
	private float[] texCoords = new float[MaxVertices * 2];
	private MeshFilter meshFilter;
	private Vector2[] modelTexCoords;

	[Header("Tiger texture mapping file")]
	public TextAsset TexCoordinatesFile;

	[Header("Tracker output data info")]
	public Vector3[] Translation = new Vector3[MAX_FACES]; 
	public Vector3[] Rotation = new Vector3[MAX_FACES];
	private bool isTracking = false;
	public int[] TrackerStatus = new int[MAX_FACES];
    private float[] translation = new float[3];
    private float[] rotation = new float[3];

    [Header("Camera settings")]
	public Material CameraViewMaterial;
	public Shader CameraViewShaderRGBA;
	public Shader CameraViewShaderBGRA;
	public Shader CameraViewShaderUnlit;
	public float CameraFocus;
	public int Orientation = 0;
	private int currentOrientation = 0;
	public int isMirrored = 1;
	private int currentMirrored = 1;
	public int camDeviceId = 0;
    private int AndroidCamDeviceId = 0;
	private int currentCamDeviceId = 0;
	public int defaultCameraWidth = -1;
	public int defaultCameraHeight = -1;
	private bool doSetupMainCamera = true;
    private bool camInited = false;

	[Header("Texture settings")]
	public int ImageWidth = 800;
	public int ImageHeight = 600;
	public int TexWidth = 512;
	public int TexHeight = 512;
#if UNITY_ANDROID
	private TextureFormat TexFormat = TextureFormat.RGB24;
#else
	private TextureFormat TexFormat = TextureFormat.RGBA32;
#endif
	private Texture2D texture = null;
	private Color32[] texturePixels;
	private GCHandle texturePixelsHandle;

	[Header("GUI button settings")]
	public Button trackingButton;
    private bool stopTrackButton = false;
    public Button portrait;
	public Button portraitUpside;
	public Button landscapeRight;
	public Button landscapeLeft;
    private Sprite trackPlay;
    private Sprite trackPause;
    private FaceEffect currentEffect = FaceEffect.Glasses;

    [HideInInspector]
    public bool frameForAnalysis = false;
    public bool frameForRecog = false;
    private bool texCoordsStaticLoaded = false;

#if UNITY_ANDROID
	private AndroidJavaObject androidCameraActivity;
	private bool AppStarted = false;
	AndroidJavaClass unity;
#endif

#endregion

#region Native code printing

    private bool enableNativePrinting = true;

	//For printing from native code
	[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
	public delegate void MyDelegate(string str);

	//Function that will be called from the native wrapper
	static void CallBackFunction(string str)
	{
		Debug.Log("::CallBack : " + str);
	}

#endregion

	private void Awake()
    {
#if PLATFORM_ANDROID && UNITY_2018_3_OR_NEWER
        if (!Permission.HasUserAuthorizedPermission(Permission.Camera))
            Permission.RequestUserPermission(Permission.Camera);
#endif

#if UNITY_WEBGL
        VisageTrackerNative._preloadFile(Application.streamingAssetsPath + "/Visage Tracker/" + ConfigFileWebGL);
        VisageTrackerNative._preloadFile(Application.streamingAssetsPath + "/Visage Tracker/" + LicenseString.licenseString);
        VisageTrackerNative._preloadAnalysisData("visageAnalysisData.js");
#endif

        // Set callback for printing from native code
        if (enableNativePrinting)
         {
         /*  MyDelegate callback_delegate = new MyDelegate(CallBackFunction);
             // Convert callback_delegate into a function pointer that can be
             // used in unmanaged code.
             IntPtr intptr_delegate = Marshal.GetFunctionPointerForDelegate(callback_delegate);
             // Call the API passing along the function pointer.
             VisageTrackerNative.SetDebugFunction(intptr_delegate);*/
         }


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
		VisageTrackerNative._initializeLicense(licenseFilePath + LicenseString.licenseString);
#endif

	}


	void Start()
	{
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
                configFilePath = Application.dataPath + "/Resources/Data/StreamingAssets/Visage Tracker/" + ConfigFileOSX;
                break;
            case RuntimePlatform.OSXEditor:
                configFilePath = Application.dataPath + "/StreamingAssets/Visage Tracker/" + ConfigFileOSX;
                break;
            case RuntimePlatform.WebGLPlayer:
                configFilePath = ConfigFileWebGL;
                break;
            case RuntimePlatform.WindowsEditor:
                configFilePath = Application.streamingAssetsPath + "/" + ConfigFileEditor;
                break;
        }

        // Initialize tracker with configuration and MAX_FACES
        trackerInited = InitializeTracker(configFilePath);

		// Get current device orientation
		Orientation = GetDeviceOrientation();

		// Open camera in native code
		camInited = OpenCamera(Orientation, camDeviceId, defaultCameraWidth, defaultCameraHeight, isMirrored);

		// Initialize various containers for scene 3D objects (glasses, tiger texture)
		InitializeContainers();

		// Load sprites for play button
		LoadButtonSprites();

		if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.OpenGLCore)
			Debug.Log("Notice: if graphics API is set to OpenGLCore, the texture might not get properly updated.");
    }


    void Update()
	{
        //signals analysis and recognition to stop if camera or tracker are not initialized and until new frame and tracking data are obtained
        frameForAnalysis = false;
        frameForRecog = false;

        if (!isTrackerReady())
            return;
      
        if (Input.GetKeyDown(KeyCode.Escape))
		{
			Application.Quit();
		}

#if (UNITY_IPHONE || UNITY_ANDROID) && UNITY_EDITOR
		// tracking will not work if the target is set to Android or iOS while in editor
		return;
#endif

		if (isTracking)
		{
#if UNITY_ANDROID
			if (VisageTrackerNative._frameChanged())
			{
				texture = null;
				doSetupMainCamera = true;
			}	
#endif
            Orientation = GetDeviceOrientation();

            // Check if orientation or camera device changed
            if (currentOrientation != Orientation || currentCamDeviceId != camDeviceId || currentMirrored != isMirrored)
			{
                currentCamDeviceId = camDeviceId;
                currentOrientation = Orientation;
                currentMirrored = isMirrored;

                // Reopen camera with new parameters 
                OpenCamera(currentOrientation, currentCamDeviceId, defaultCameraWidth, defaultCameraHeight, currentMirrored);
				texture = null;
				doSetupMainCamera = true;

			}

            // grab current frame and start face tracking
            VisageTrackerNative._grabFrame();

			VisageTrackerNative._track();
            VisageTrackerNative._getTrackerStatus(TrackerStatus);

            //After the track has been preformed on the new frame, the flags for the analysis and recognition are set to true
            frameForAnalysis = true;
            frameForRecog = true;

            // Set main camera field of view based on camera information
            if (doSetupMainCamera)
			{
				// Get camera information from native
				VisageTrackerNative._getCameraInfo(out CameraFocus, out ImageWidth, out ImageHeight);
				float aspect = ImageWidth / (float)ImageHeight;
				float yRange = (ImageWidth > ImageHeight) ? 1.0f : 1.0f / aspect;
				Camera.main.fieldOfView = Mathf.Rad2Deg * 2.0f * Mathf.Atan(yRange / CameraFocus);
				doSetupMainCamera = false;
			}
		}

		RefreshImage();

        for (int i = 0; i < TrackerStatus.Length; ++i)
        {
            if (TrackerStatus[i] == (int)TrackStatus.OK)
            {
                UpdateControllableObjects(i);

                if(!texCoordsStaticLoaded)
                {
                    texCoordsStaticLoaded = GetTextureCoordinates(out modelTexCoords);
                }
            }
            else
                ResetControllableObjects(i);
        }             
	}

    bool isTrackerReady()
    {
        if (camInited && trackerInited && !stopTrackButton)
        {    
            isTracking = true;
            trackingButton.image.overrideSprite = trackPause;
        }
        else
        {
            isTracking = false;
        }
        return isTracking;
    }

    void OnDestroy()
	{
#if UNITY_ANDROID
		this.androidCameraActivity.Call("closeCamera");
#else
		camInited = !(VisageTrackerNative._closeCamera());
#endif
	}

#if UNITY_IPHONE
	void OnApplicationPause(bool pauseStatus) {
		if(pauseStatus){
			camInited = !(VisageTrackerNative._closeCamera());
			isTracking = false;
		}
		else
		{
			camInited = OpenCamera(Orientation, camDeviceId, defaultCameraWidth, defaultCameraHeight, isMirrored);
			isTracking = true;
		}
	}
#endif

#region GUI Buttons OnClick events
	public void onButtonEffect()
	{
		currentEffect = (currentEffect == FaceEffect.Glasses) ? FaceEffect.Tiger : FaceEffect.Glasses;
	}

	public void onButtonPlay()
	{
		if (!isTracking)
		{
            stopTrackButton = false;
            trackingButton.image.overrideSprite = trackPause;
			isTracking = true;
		}
		else
		{
            stopTrackButton = true;
            trackingButton.image.overrideSprite = trackPlay;
			isTracking = false;
		}
	}

	public void onButtonPortrait()
	{
        Orientation = 0;
	}

	public void onButtonPortraitUpside()
	{
        Orientation = 2;
	}

	public void onButtonLandscapeLeft()
	{
        Orientation = 3;
	}

	public void onButtonLandscapeRight()
	{
        Orientation = 1;
	}

	public void onButtonSwitch()
	{
#if UNITY_WEBGL
            camInited = false;
            VisageTrackerNative._openCamera(ImageWidth, ImageHeight, isMirrored, "OnSuccessCallbackCamera", "OnErrorCallbackCamera");
#else
        camDeviceId = (camDeviceId == 1) ? 0 : 1;
#endif
	}
#endregion


	/// <summary>
	/// Initialize tracker with maximum number of faces - MAX_FACES.
	/// Additionally, depending on a platform set an appropriate shader.
	/// </summary>
	/// <param name="config">Tracker configuration path and name.</param>
	bool InitializeTracker(string config)
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
		this.androidCameraActivity = unity.GetStatic<AndroidJavaObject>("currentActivity");
#elif UNITY_STANDALONE_WIN || UNITY_WEBGL
		Shader shader = Shader.Find("Custom/RGBATex");
		CameraViewMaterial.shader = shader;
#else
        Shader shader = Shader.Find("Custom/BGRATex");
		CameraViewMaterial.shader = shader;
#endif

#if UNITY_WEBGL
        // initialize tracker
        VisageTrackerNative._initTracker(config, MAX_FACES, "CallbackInitTracker");
        return trackerInited;
#else
        VisageTrackerNative._initTracker(config, MAX_FACES);      
        return true;
#endif
    }

#region Callback Function for WEBGL

    public void CallbackInitTracker()
    {
        Debug.Log("TrackerInited");
        trackerInited = true;
    }

    public void OnSuccessCallbackCamera()
    {
        Debug.Log("CameraSuccess");
        camInited = true;
    }

    public void OnErrorCallbackCamera()
    {
        Debug.Log("CameraError");
    }

#endregion

    /// <summary>
	/// Update Unity texture with frame data from native camera.
	/// </summary>
	void RefreshImage()
	{
		// Initialize texture
		if (texture == null && isTracking && ImageWidth > 0)
		{
			TexWidth = Convert.ToInt32(Math.Pow(2.0, Math.Ceiling(Math.Log(ImageWidth) / Math.Log(2.0))));
			TexHeight = Convert.ToInt32(Math.Pow(2.0, Math.Ceiling(Math.Log(ImageHeight) / Math.Log(2.0))));
			texture = new Texture2D(TexWidth, TexHeight, TexFormat, false);

			var cols = texture.GetPixels32();
			for (var i = 0; i < cols.Length; i++)
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

		if (texture != null && isTracking && TrackerStatus[0] != (int)TrackStatus.OFF)
		{
#if UNITY_STANDALONE_WIN
			// send memory address of textures' pixel data to VisageTrackerUnityPlugin
			VisageTrackerNative._setFrameData(texturePixelsHandle.AddrOfPinnedObject());
			((Texture2D)texture).SetPixels32(texturePixels, 0);
			((Texture2D)texture).Apply();
#elif UNITY_IPHONE || UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX || UNITY_ANDROID
			if (SystemInfo.graphicsDeviceVersion.StartsWith ("Metal"))
				VisageTrackerNative._bindTextureMetal (texture.GetNativeTexturePtr ());
			else
				VisageTrackerNative._bindTexture ((int)texture.GetNativeTexturePtr ());
#elif UNITY_WEBGL
            VisageTrackerNative._bindTexture(texture.GetNativeTexturePtr());
#endif
        }
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
	int GetDeviceOrientation()
	{
		int devOrientation;

#if UNITY_ANDROID
		//Device orientation is obtained in AndroidCameraPlugin so we only need information about whether orientation is changed
		int oldWidth = ImageWidth;
		int oldHeight = ImageHeight;

		VisageTrackerNative._getCameraInfo(out CameraFocus, out ImageWidth, out ImageHeight);

		if ((oldWidth!=ImageWidth || oldHeight!=ImageHeight) && ImageWidth != 0 && ImageHeight !=0 && oldWidth != 0 && oldHeight !=0 )
			devOrientation = (Orientation ==1) ? 0:1;
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
		this.androidCameraActivity.Call("runOnUiThread", new AndroidJavaRunnable(() => {
			this.androidCameraActivity.Call("closeCamera");
			this.androidCameraActivity.Call("GrabFromCamera", width, height, camDeviceId);
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
    private void UpdateControllableObjects(int faceIndex)
    { 
        TriangleNumber = VisageTrackerNative._getFaceModelTriangleCount();
        VertexNumber = VisageTrackerNative._getFaceModelVertexCount();
        meshFilter.mesh.Clear();

		if (currentEffect == FaceEffect.Tiger)
		{
			for (int i = 0; i < Math.Min(ControllableObjectsGlasses.Length, MAX_FACES); i++)
				ControllableObjectsGlasses[i].transform.position -= new Vector3(0, 0, 10000);
		}
		else if (currentEffect == FaceEffect.Glasses)
		{
			for (int i = 0; i < Math.Min(ControllableObjectsTiger.Length, MAX_FACES); i++)
				ControllableObjectsTiger[i].transform.position -= new Vector3(0, 0, 10000);
		}

		// update translation and rotation
	
        VisageTrackerNative._getHeadTranslation(translation, faceIndex);
        VisageTrackerNative._getHeadRotation(rotation, faceIndex);
        //
        Translation[faceIndex].x = translation[0];
        Translation[faceIndex].y = translation[1];
        Translation[faceIndex].z = translation[2];

        Rotation[faceIndex].x = rotation[0];

        if ((Application.platform == RuntimePlatform.WindowsEditor || Application.platform == RuntimePlatform.WindowsPlayer) && isMirrored == 1)
        {
            Rotation[faceIndex].y = -rotation[1];
            Rotation[faceIndex].z = -rotation[2];
        }
        else
        {
            Rotation[faceIndex].y = rotation[1];
            Rotation[faceIndex].z = rotation[2];
        }

        Transform3DData(faceIndex);
        //
        if (currentEffect == FaceEffect.Glasses)
		{
			// Update glasses position
			if (faceIndex < ControllableObjectsGlasses.Length)
			{
				ControllableObjectsGlasses[faceIndex].transform.position = startingPositionsGlasses[faceIndex] + Translation[faceIndex];
				ControllableObjectsGlasses[faceIndex].transform.rotation = Quaternion.Euler(startingRotationsGlasses[faceIndex] + Rotation[faceIndex]);

                if ((Application.platform == RuntimePlatform.WindowsEditor || Application.platform == RuntimePlatform.WindowsPlayer) && isMirrored == 1)
                {
                    ControllableObjectsGlasses[faceIndex].transform.position = Vector3.Scale(ControllableObjectsGlasses[faceIndex].transform.position, new Vector3(-1, 1, 1));
                }

           }

		}
		else
		{
            VisageTrackerNative._getFaceModelVertices(vertices, faceIndex);
            VisageTrackerNative._getFaceModelTriangles(triangles, faceIndex);
                
			if (faceIndex < ControllableObjectsTiger.Length)
			{
				// Get mesh vertices
				if (Vertices[faceIndex] == null || Vertices[faceIndex].Length != VertexNumber)
					Vertices[faceIndex] = new Vector3[VertexNumber];

				for (int j = 0; j < VertexNumber; j++)
				{
					Vertices[faceIndex][j] = new Vector3(vertices[j * 3 + 0], vertices[j * 3 + 1], vertices[j * 3 + 2]);
				}

						// Get mesh triangles
						if (Triangles.Length != TriangleNumber)
							Triangles = new int[TriangleNumber * 3];

						for (int j = 0; j < TriangleNumber * 3; j++)
						{
							Triangles[j] = triangles[j];
						}

						// Get mesh texture coordinates
						if (TexCoords.Length != VertexNumber)
					TexCoords = new Vector2[VertexNumber];

				for (int j = 0; j < VertexNumber; j++)
				{
					TexCoords[j] = new Vector2(modelTexCoords[j].x, modelTexCoords[j].y);
				}

				MeshFilter meshFilter = ControllableObjectsTiger[faceIndex].GetComponent<MeshFilter>();
				meshFilter.mesh.vertices = Vertices[faceIndex]; //needs to be obtained for each face
				meshFilter.mesh.triangles = Triangles; //not changing 
				meshFilter.mesh.uv = TexCoords; // tiger texture coordinates
				meshFilter.mesh.uv2 = TexCoords; // tiger texture coordinates
				meshFilter.mesh.RecalculateNormals();
				meshFilter.mesh.RecalculateBounds();				
			}
				
            // Update mesh position
            ControllableObjectsTiger[faceIndex].transform.position = startingPositionsTiger[faceIndex] + Translation[faceIndex];
            
            ControllableObjectsTiger[faceIndex].transform.rotation = Quaternion.Euler(startingRotationsTiger[faceIndex] + Rotation[faceIndex]);

            if ((Application.platform == RuntimePlatform.WindowsEditor || Application.platform == RuntimePlatform.WindowsPlayer) && isMirrored == 1)
            {
                ControllableObjectsTiger[faceIndex].transform.position = Vector3.Scale(ControllableObjectsTiger[faceIndex].transform.position, new Vector3(-1, 1, 1));
                ControllableObjectsTiger[faceIndex].transform.localScale = new Vector3(1, 1, 1);

            }
        }  
    }

    /// <summary>
    /// Reset data in controllable objects (glasses, tiger mesh) when tracker is not tracking.
    /// </summary>
    public void ResetControllableObjects(int faceIndex)
    {
        ControllableObjectsGlasses[faceIndex].transform.position = startingPositionsGlasses[faceIndex] + new Vector3(-10000, -10000, -10000);
        ControllableObjectsGlasses[faceIndex].transform.rotation = Quaternion.Euler(startingRotationsGlasses[faceIndex] + new Vector3(0,0,0));

        ControllableObjectsTiger[faceIndex].transform.position = startingPositionsTiger[faceIndex] + new Vector3(-10000, -10000, -10000);
        ControllableObjectsTiger[faceIndex].transform.rotation = Quaternion.Euler(startingRotationsTiger[faceIndex] + new Vector3(0, 0, 0));
    }

    /// <summary>
    /// Helper function for transforming data obtained from tracker
    /// </summary>
    public void Transform3DData(int i)
    {
        Translation[i].x *= (-1);
        Rotation[i].x = 180.0f * Rotation[i].x / 3.14f;
        Rotation[i].y += 3.14f;
        Rotation[i].y = 180.0f * (-Rotation[i].y) / 3.14f;
        Rotation[i].z = 180.0f * (-Rotation[i].z) / 3.14f;
    }

    /// <summary>
    /// Initialize arrays used for controllable objects (glasses, tiger texture)
    /// </summary>
    void InitializeContainers()
	{

		// Initialize translation and rotation arrays
		for (int i = 0; i < MAX_FACES; i++)
		{
			Translation[i] = new Vector3(0, 0, -1000);
			Rotation[i] = new Vector3(0, 0, -1000);
		}

		// Initialize arrays for controllable objects
		startingPositionsGlasses = new Vector3[ControllableObjectsGlasses.Length];
		startingRotationsGlasses = new Vector3[ControllableObjectsGlasses.Length];

		startingPositionsTiger = new Vector3[ControllableObjectsTiger.Length];
		startingRotationsTiger = new Vector3[ControllableObjectsTiger.Length];


		for (int i = 0; i < ControllableObjectsGlasses.Length; i++)
		{
			startingPositionsGlasses[i] = ControllableObjectsGlasses[i].transform.position;
			startingRotationsGlasses[i] = ControllableObjectsGlasses[i].transform.rotation.eulerAngles;
			ControllableObjectsGlasses[i].transform.position -= new Vector3(0, 0, 10000);
		}

		for (int i = 0; i < ControllableObjectsTiger.Length; i++)
		{
			startingPositionsTiger[i] = ControllableObjectsTiger[i].transform.position;
			startingRotationsTiger[i] = ControllableObjectsTiger[i].transform.rotation.eulerAngles;
		}
	}


	/// <summary>
	/// Load sprites for GUI play button
	/// </summary>
	private void LoadButtonSprites()
	{
		trackPlay = Resources.Load<Sprite>("play");
		trackPause = Resources.Load<Sprite>("pause");
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

#if UNITY_ANDROID
	void Unzip()
	{
		string[] pathsNeeded = {
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
		string outputDir;
		string localDataFolder = "Visage Tracker";

		outputDir = Application.persistentDataPath;

		if (!Directory.Exists(outputDir))
		{
			Directory.CreateDirectory(outputDir);
		}
		foreach (string filename in pathsNeeded)
		{
			WWW unpacker = new WWW("jar:file://" + Application.dataPath + "!/assets/" + localDataFolder + "/" + filename);

			while (!unpacker.isDone) { }

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
}
