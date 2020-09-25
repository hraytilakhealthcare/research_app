using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;


public partial class Analyser: MonoBehaviour
{
#if !UNITY_WEBGL
    #region Properties

    bool AnalyserInitialized = false;

    [Header("Face Analysis output data")]
    const int NUM_EMOTIONS = 7;
    private const double EMO_DISPLAY_TRESHOLD = 0.3;
    private float[] age = new float[Tracker.MAX_FACES];
    private int[] gender = new int[Tracker.MAX_FACES];
    private List<float[]> EmotionListPerFace = new List<float[]>();

    [Header("Tracker object settings")]
    private int[] TrackerStatus = new int[Tracker.MAX_FACES];
    private Vector3[] Translation = new Vector3[Tracker.MAX_FACES];
    private Vector3[] Rotation = new Vector3[Tracker.MAX_FACES];
    private float[] translation = new float[3];
    private float[] rotation = new float[3];

    [Header("Analysis Data Element")]
    List<GameObject> AnalysisList = new List<GameObject>();

    [Header("Face Analysis filters' parameters")]
    private float[] currMaxTrackingQuality = new float[Tracker.MAX_FACES];

    private float[] TrackingQualityDELTA = new float[Tracker.MAX_FACES];

    List<List<float>> TrackingQualityList = new List<List<float>>();
    private int[] ageFilterFramesReached = new int[Tracker.MAX_FACES];

    //Filter
    List<List<float>> AgeFilterList = new List<List<float>>();
    List<List<int>> GenderFilterList = new List<List<int>>();
    List<List<float[]>> EmotionFilterList = new List<List<float[]>>();
    //
    int numFilterFrames = 1;
    int emotionFilterTime = 500;
    int genderFilterTime = 5000;
    int ageFilterFrames = 10;

    int[] ageFilterCount = new int[Tracker.MAX_FACES];
    int[] GenderSmoothed = new int[Tracker.MAX_FACES];
    float[] AgeSmoothed = new float[Tracker.MAX_FACES];
    List<float[]> EmotionsSmoothed = new List<float[]>();

    //Gender display variables
    private Material[] matrialList;
    private Material[] warningList;

    //Emotion display variables
    private Vector3[][] emoVertices = new Vector3[Tracker.MAX_FACES][];
    private float EmoBarSize;
    private TextMesh[][] percentage = new TextMesh[Tracker.MAX_FACES][];

    FDP[] fdpArray = new FDP[Tracker.MAX_FACES];
    private float[] rawfdp = new float[2000];

    //Background
    private BackgroundWorker fa_worker;
    private System.Object lockAnalyser = new System.Object();
    private System.Object lockWithinConstraints = new System.Object();

    #endregion

    /// <summary>
    /// Wait for background thread to finish before quitting.
    /// </summary>
    private void OnApplicationQuit()
    {
        if (fa_worker.WorkerSupportsCancellation == true && fa_worker.IsBusy)
        {
            StartCoroutine(StopAgeGenderEmotionsEstimation());
        }
    }

    // Use this for initialization
    void Start()
    {
        string dataFilesPath = Application.streamingAssetsPath + "/" + "Visage Tracker/bdtsdata/LBF/vfadata";

        switch (Application.platform)
        {
            case RuntimePlatform.IPhonePlayer:

                break;
			case RuntimePlatform.Android: 
			dataFilesPath = Application.persistentDataPath + "/" + "bdtsdata/LBF/vfadata";

                break;
            case RuntimePlatform.OSXPlayer:

                break;
            case RuntimePlatform.OSXEditor:

                break;
            case RuntimePlatform.WebGLPlayer:
              
                break;
            case RuntimePlatform.WindowsEditor:
                dataFilesPath = Application.streamingAssetsPath + "/Visage Tracker/bdtsdata/LBF/vfadata";
                break;
        }

        // Initialize analysis 
        AnalyserInitialized = InitializeAnalyser(dataFilesPath);

        // Intialize arrays used for face analysis
        InitializeContainers();

        // Initialize translation and rotation arrays
        for (int i = 0; i < Tracker.MAX_FACES; i++)
        {
            Translation[i] = new Vector3(0, 0, -1000);
            Rotation[i] = new Vector3(0, 0, -1000);
            EmotionListPerFace.Add(default(float[]));
            EmotionFilterList.Add(new List<float[]>());
            EmotionsSmoothed.Add(default(float[]));
        }

        warningPanel.SetActive(false);
    }

    /// <summary>
	/// Intialize arrays used for face analysis.
	/// </summary>
    private void InitializeContainers()
    {
        AgeSmoothed = new float[Tracker.MAX_FACES];
        GenderSmoothed = new int[Tracker.MAX_FACES];

        for (int i = 0; i < Tracker.MAX_FACES; ++i)
        {
            TrackingQualityList.Add(new List<float>());

            AgeFilterList.Add(new List<float>());
            GenderFilterList.Add(new List<int>());
            ageFilterCount[i] = 0;
            GenderSmoothed[i] = -1;
            AgeSmoothed[i] = -1;

            EmotionFilterList.Add(new List<float[]>());
            EmotionsSmoothed.Add(default(float[]));

            currMaxTrackingQuality[i] = 0.7f;
            TrackingQualityDELTA[i] = 0.0f;
            ageFilterFramesReached[i] = 0;

            AnalysisList.Add((GameObject)Instantiate(AnalysisDataElement));
            AnalysisList[i].GetComponent<MeshFilter>().transform.position -= new Vector3(0, 0, 10000);

            fdpArray[i] = new FDP();

            emoVertices[i] = AnalysisList[i].GetComponentsInChildren<MeshFilter>()[3 + i].mesh.vertices;

            percentage[i] = new TextMesh[NUM_EMOTIONS];

            for (int j = 0; j < NUM_EMOTIONS; ++j)
            {
                percentage[i][j] = AnalysisList[i].GetComponentsInChildren<TextMesh>()[1 + j];
            }
        }

        matrialList = Resources.LoadAll("Analysis/Material", typeof(Material)).Cast<Material>().ToArray();

        warningList = Resources.LoadAll("WarningPanel/Material", typeof(Material)).Cast<Material>().ToArray();

        EmoBarSize = Math.Abs(emoVertices[0][0].x - emoVertices[0][3].x);
    }

    
    /// <summary>
    /// Initialize analyser with path to the data 
    /// </summary>
    /// <param name="dataPath">Path to the folder containing the VisageFaceAnalyser data file.s</param>
    bool InitializeAnalyser(string dataPath)
    {  
        int is_initialized = VisageTrackerNative._initAnalyser(dataPath);

        if ((is_initialized & (int)VFA_FLAGS.VFA_AGE) != (int)VFA_FLAGS.VFA_AGE)
        {
            return false;
        }

        if ((is_initialized & (int)VFA_FLAGS.VFA_EMOTION) != (int)VFA_FLAGS.VFA_EMOTION)
        {
            return false;
        }

        if ((is_initialized & (int)VFA_FLAGS.VFA_GENDER) != (int)VFA_FLAGS.VFA_GENDER)
        {
            return false;
        }

        return true;      
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (fa_worker.WorkerSupportsCancellation == true)
            {
                StartCoroutine(StopAgeGenderEmotionsEstimation());
            }
            Application.Quit();
        }

        if (AnalyserInitialized && Tracker.frameForAnalysis)
        {

            if (!VisageTrackerNative._prepareDataForAnalysis())
                return;

            UpdateAnalysisResults();
        }
    }

    /// <summary>
	/// Performs face analysis in a separate thread.
	/// </summary>
	/// <param name="sender"></param>
	/// <param name="e"></param>
    private void worker_DoWorkFaceAnalysis(object sender, DoWorkEventArgs e)
    {
        List<object> argumentList = e.Argument as List<object>;
        //read and cast the parameters
        int[] trackerStatus = (int[])argumentList[0];

        for (int i = 0; i < Tracker.MAX_FACES; i++)
        {
            if (trackerStatus[i] == (int)TrackStatus.OK)
            {
                EstimateAge(i);
                EstimateGender(i);
                EstimateEmotion(i);

            }
            else if (trackerStatus[i] != (int)TrackStatus.OK)
            {
                ResetEmotionFilter(i);
                ResetAgeFilterParameters(i);
                ResetGenderFilterParameters(i);
            }
        }
    }

    IEnumerator StopAgeGenderEmotionsEstimation()
    {
        if (fa_worker.IsBusy)
        {
            fa_worker.CancelAsync();
        }

        while (fa_worker.IsBusy)
        {
            yield return new WaitForSeconds(0.04f);
        }
    }

    private void UpdateAnalysisResults()
    {
        if (fa_worker != null)
        {
            if (fa_worker.IsBusy)
            {
                return;
            }
        }
        else
        {
            fa_worker = new BackgroundWorker();
            fa_worker.DoWork += worker_DoWorkFaceAnalysis;
            fa_worker.WorkerSupportsCancellation = true;
        }

        VisageTrackerNative._getTrackerStatus(TrackerStatus);

        //Start tracking from image in another thread
        List<object> arguments = new List<object>();
        arguments.Add(TrackerStatus);
        fa_worker.RunWorkerAsync(arguments);

        for (int i = 0; i < Tracker.MAX_FACES; i++)
        {
            if (TrackerStatus[i] != (int)TrackStatus.OK)
            {   
                RemoveAnalysisDataElement(i);
            }

            if (TrackerStatus[i] == (int)TrackStatus.OK)
            {           
                lock (lockAnalyser)
                {
                    DrawEmotions(i, EmotionsSmoothed[i]);
                    DrawGender(i, GenderSmoothed[i]);
                    DrawAge(i, AgeSmoothed[i]);
                }

                DisplayAnalysisResults(i);
            } 
        }
    }

    /// <summary>
    /// Removes element with analysis data.
    /// </summary>
    /// <param name="faceIndex">Index for the particular tracked face.</param>
    private void RemoveAnalysisDataElement(int faceIndex)
    {
          AnalysisList[faceIndex].GetComponent<MeshFilter>().transform.position -= new Vector3(0, 0, 10000);
    }


    /// <summary>
    /// Performs age analysis for given face (faceIndex).
    /// Age is estimated if current tracking quality is greater than current max tracking quality increased by 5*k% (where k is value between 1 and 10), less than 10 age values is collected and face position is frontal. 
    /// After 10 age values is collected, current max tracking quality is changed. Age will be estimated only if the same conditions are satisfied, except the current tracking quality 
    /// has to be greater than current max tracking quality increased by 0.5*k% (where k is an integer).
    /// Results are saved to AgeSmoothed array for a faceIndex face.
    /// This function is called from separated thread. 
    /// </summary>
    /// <param name="faceIndex">Index of the tracker/face for which age analysis will be performed.</param>
    private void EstimateAge(int faceIndex)
    {
        float currentTrackingQuality = VisageTrackerNative._getTrackingQuality(faceIndex);

        lock (lockWithinConstraints)
        {
            if (currMaxTrackingQuality[faceIndex] * (1.0f + TrackingQualityDELTA[faceIndex]) <= currentTrackingQuality && ageFilterCount[faceIndex] <= ageFilterFrames && withinConstraints(faceIndex))
            {
                age[faceIndex] = VisageTrackerNative._estimateAge(faceIndex);
               
                if (age[faceIndex] == -1)
                    return;

                TrackingQualityList[faceIndex].Add(currentTrackingQuality);
                AgeFilterList[faceIndex].Add(age[faceIndex]);
                // 
                ageFilterCount[faceIndex]++;

                TrackingQualityDELTA[faceIndex] += DeltaStep(ageFilterFramesReached[faceIndex]);
            }
        }

        lock (lockAnalyser)
        {
            AgeSmoothed[faceIndex] = (int)AgeFilterList[faceIndex].Average();
        }

        if (ageFilterCount[faceIndex] == ageFilterFrames)
        {
            ageFilterCount[faceIndex] = 0;
            currMaxTrackingQuality[faceIndex] = TrackingQualityList[faceIndex].Max();
            TrackingQualityDELTA[faceIndex] = 0.0f;
            ageFilterFramesReached[faceIndex]++;
        }
    }

    /// <summary>
    /// Determines the value of the TrackingQualityDELTA parameter according to number of collected age values.
    /// If parameter ageFilterFramesReached is equal to 0 (i.e. less than ageFilterFrames age values is collected) than the delta step value is increased by 0.05,
    /// while if it is greater than 0 (i.e. more than ageFilterFrames age values is collected) the delta step value will increase by 0.005.
    /// </summary>
    /// <param name="ageFilterFramesReached">Parameter that indicates whether ageFilterFrames frames are collected.</param>
    private float DeltaStep(int ageFilterFramesReached)
    {
        if (ageFilterFramesReached == 0)
        {
            return 0.05f;
        }
        else
            return 0.005f;
    }

    /// <summary>
    /// Performs emotion analysis for given face (faceIndex). This function should be called when
    /// tracker status is TrackStatus.OK. 
    /// Additional filtering is applied to emotion to reduce jitter:
    ///  - emotionFilterTime - control the filter window in milliseconds
    /// Results are saved to EmotionsSmoothed List for a faceIndex face.
    /// This function is called from separated thread. 
    /// </summary>
    /// <param name="faceIndex">Index of the tracker/face for which the emotion analysis is to be performed</param>
    private void EstimateEmotion(int faceIndex)
    {
        lock (lockAnalyser)
        {
            if (EmotionsSmoothed[faceIndex] == default(float[]))
                EmotionsSmoothed[faceIndex] = new float[NUM_EMOTIONS];
        }

        lock (lockWithinConstraints)
        {
            if (withinConstraints(faceIndex))
            {
                EmotionListPerFace[faceIndex] = new float[NUM_EMOTIONS];
                //
                VisageTrackerNative._estimateEmotion(EmotionListPerFace[faceIndex], faceIndex);
                //
                EmotionFilterList[faceIndex].Add(EmotionListPerFace[faceIndex]);
                //
                float FrameRate = VisageTrackerNative._getFrameRate();
                //
                numFilterFrames = (int)Math.Round(emotionFilterTime * FrameRate / 1000);
                //
                if (EmotionFilterList[faceIndex].Count > numFilterFrames)
                {
                    for (int emoIndex = 0; emoIndex < NUM_EMOTIONS; emoIndex++)
                    {
                        float avgEmotion = 0;
                        for (int j = 0; j < EmotionFilterList[faceIndex].Count; j++)
                        {
                            avgEmotion += EmotionFilterList[faceIndex][j][emoIndex];
                        }

                        avgEmotion = avgEmotion / EmotionFilterList[faceIndex].Count();
                        lock (lockAnalyser)
                        {
                            EmotionsSmoothed[faceIndex][emoIndex] = avgEmotion;
                        }
                    }

                    EmotionFilterList[faceIndex].RemoveAt(0);
                }
            }
        }
    }


    /// <summary>
    /// Performs gender analysis for given face (faceIndex). 
    /// Results are saved to GenderSmoothed array for a faceIndex face.
    /// This function is called from separated thread. 
    /// </summary>
    /// <param name="faceIndex">Index of the tracker/face for which gender analysis is to be performed</param>
    private void EstimateGender(int faceIndex)
    {
        float FrameRate = VisageTrackerNative._getFrameRate();

        numFilterFrames = (int)Math.Round(genderFilterTime * FrameRate / 1000);

        lock (lockWithinConstraints)
        {
            if (withinConstraints(faceIndex))
            {
                gender[faceIndex] = VisageTrackerNative._estimateGender(faceIndex);
                GenderFilterList[faceIndex].Add(gender[faceIndex]);
            }
        }
       
        lock (lockAnalyser)
        {
            GenderSmoothed[faceIndex] = (GenderFilterList[faceIndex].Count != 0) ? (int)Math.Round(GenderFilterList[faceIndex].Average()) : -1;
        }

        if (GenderFilterList[faceIndex].Count > numFilterFrames)
        {
            lock (lockAnalyser)
            {
                GenderSmoothed[faceIndex] = (int)Math.Round(GenderFilterList[faceIndex].Average());
            }

            GenderFilterList[faceIndex].RemoveAt(0);
        }    
    }


    /// <summary>
    /// Resets filter parameters for age.
    /// Typical usage is when tracker status has changed from TrackStatus.OK to TrackStatus.INIT
    /// </summary>
    /// <param name="faceIndex">Index for the particular tracked face.</param>
    private void ResetAgeFilterParameters(int faceIndex)
    {
        currMaxTrackingQuality[faceIndex] = 0.7f;
        TrackingQualityDELTA[faceIndex] = 0.0f;
        ageFilterFramesReached[faceIndex] = 0;
        //
        age[faceIndex] = -1;
        //
        AgeSmoothed[faceIndex] = -1;
        //
        AgeFilterList[faceIndex].Clear();
        //
        TrackingQualityList[faceIndex].Clear();
        //
        ageFilterCount[faceIndex] = 0;
    }

    /// <summary>
    /// Resets emotion filters used to reduce jitter for faceIndex tracker/face.
    /// Typical usage is when tracker status has changed from TrackStatus.OK to TrackStatus.INIT
    /// </summary>
    /// <param name="faceIndex">Index of the tracker/face for which emotion analysis is to be performed</param>
    private void ResetEmotionFilter(int faceIndex)
    {
        EmotionsSmoothed[faceIndex] = default(float[]);
        EmotionFilterList[faceIndex].Clear();
    }

    /// <summary>
    /// Resets filter parameters for gender.
    /// Typical usage is when tracker status has changed from TrackStatus.OK to TrackStatus.INIT
    /// </summary>
    /// <param name="faceIndex">Index for the particular tracked face.</param>
    private void ResetGenderFilterParameters(int faceIndex)
    {
        gender[faceIndex] = -1;
        //
        GenderSmoothed[faceIndex] = -1;
        //
        GenderFilterList[faceIndex].Clear();
    }

    /// <summary>
    /// Adjust emotion bars according to emotion analysis values for a given tracker/face (faceIndex).
    /// If the passed Emotions array is null, no bars will be drawn.
    /// </summary>
    /// <param name="faceIndex">Index of the tracker/face for which to draw emotions</param>
    /// <param name="Emotions">Float array containing emotions</param>
    private void DrawEmotions(int faceIndex, float[] Emotions)
    {
        for (int emoIndex = 0; emoIndex < NUM_EMOTIONS; emoIndex++)
        {
            emoVertices[faceIndex][0].x = emoVertices[faceIndex][2].x;
            emoVertices[faceIndex][1].x = emoVertices[faceIndex][2].x;

            if (Emotions == null)
                return;

            float EmoBarLength = EmoBarSize * Emotions[emoIndex];
            if (Emotions[emoIndex] > EMO_DISPLAY_TRESHOLD)
            {
                emoVertices[faceIndex][0].x = emoVertices[faceIndex][2].x + EmoBarLength;
                emoVertices[faceIndex][1].x = emoVertices[faceIndex][2].x + EmoBarLength;
            }
            AnalysisList[faceIndex].GetComponentsInChildren<MeshFilter>()[3 + emoIndex].mesh.vertices = emoVertices[faceIndex];

            if (Emotions[emoIndex] > EMO_DISPLAY_TRESHOLD)
            {
                percentage[faceIndex][emoIndex].text = ((int)(Emotions[emoIndex] * 100)).ToString() + "%";
            }
            else
            {
                percentage[faceIndex][emoIndex].text = " ";
            }
        }

    }

    /// <summary>
    /// Adjust material on gender mesh according to gender analysis values for a given tracker/face (faceIndex).
    /// </summary>
    /// <param name="faceIndex">Index of the tracker/face for which to draw gemder</param>
    /// <param name="gender">0 for female, 1 for male</param>
    private void DrawGender(int faceIndex, int gender)
    {
        AnalysisList[faceIndex].GetComponentsInChildren<Renderer>()[3].enabled = true;
        if (gender == 0)
        {
            AnalysisList[faceIndex].GetComponentsInChildren<Renderer>()[3].GetComponentsInChildren<Renderer>()[0].material = matrialList[0];
            AnalysisList[faceIndex].GetComponentsInChildren<Renderer>()[3].GetComponentsInChildren<Renderer>()[0].enabled = true;
        
        }
        else if (gender == 1)
        {
            AnalysisList[faceIndex].GetComponentsInChildren<Renderer>()[3].GetComponentsInChildren<Renderer>()[0].material = matrialList[1];
            AnalysisList[faceIndex].GetComponentsInChildren<Renderer>()[3].GetComponentsInChildren<Renderer>()[0].enabled = true;
          
        }
        else
        {
            AnalysisList[faceIndex].GetComponentsInChildren<Renderer>()[3].enabled = false;
        }
    }

    /// <summary>
    /// Adjust text on age text mesh to age value for a given tracker/face (faceIndex).
    /// </summary>
    /// <param name="faceIndex">Index of the tracker/face for which to draw age</param>
    /// <param name="age">estimated age for given faceIndex</param>
    private void DrawAge(int faceIndex, float age)
    {
        if (age > 0)
        {
            AnalysisList[faceIndex].GetComponentsInChildren<Renderer>()[1].GetComponent<TextMesh>().text = age + "y";
        }
        else
        {
            AnalysisList[faceIndex].GetComponentsInChildren<Renderer>()[1].GetComponent<TextMesh>().text = " - ";
        }
    }

    /// <summary>
    /// Calculates the chin point position and sets analysis data element at that position for a given tracker/face (faceIndex).
    /// </summary>
    /// <param name="faceIndex">Index of the tracker/face for which analysis data will be displayed</param>
    private void DisplayAnalysisResults(int faceIndex)
    {
        float[] positions = new float[3];

        VisageTrackerNative._getAllFeaturePoints3D(rawfdp, rawfdp.Length, faceIndex);

        fdpArray[faceIndex].Fill(rawfdp);

        Vector3[] positions3D = new Vector3[Tracker.MAX_FACES];

        // Get position of chin point - 2.1
        positions = fdpArray[faceIndex].getFPPos(2, 1);

        positions3D[faceIndex].x = -positions[0];
        positions3D[faceIndex].y = positions[1];
        positions3D[faceIndex].z = positions[2];

        AnalysisList[faceIndex].GetComponent<MeshFilter>().transform.position = positions3D[faceIndex];
        if ((Application.platform == RuntimePlatform.WindowsEditor || Application.platform == RuntimePlatform.WindowsPlayer) && Tracker.isMirrored == 1)
        {
            AnalysisList[faceIndex].GetComponent<MeshFilter>().transform.position = Vector3.Scale(AnalysisList[faceIndex].GetComponent<MeshFilter>().transform.position, new Vector3(-1, 1, 1));
        }
        lock (lockWithinConstraints)
        {
            if (!withinConstraints(faceIndex))
            {
                warningPanel.GetComponentInChildren<Image>().material = warningList[0];
                warningPanel.SetActive(true);
                warningPanelImage.color = new Color(1.0f, 1.0f, 1.0f, Mathf.Lerp(warningPanelImage.color.a, 235 / 255f, Time.deltaTime * 3f));
            }
            else
            {
                warningPanelImage.color = new Color(1.0f, 1.0f, 1.0f, Mathf.Lerp(warningPanelImage.color.a, 0.0f, Time.deltaTime * 3f));
            }
        }
    }

    /// <summary>
    /// Compares obtained translation and rotation data with set constraints if data is within the limits returns true, otherwise returns false
    /// </summary>
    /// <param name="faceIndex">Index of the tracker/face for which analysis data will be displayed</param>
    public bool withinConstraints(int faceIndex)
    {
        VisageTrackerNative._getHeadTranslation(translation, faceIndex);
        VisageTrackerNative._getHeadRotation(rotation, faceIndex);

        Translation[faceIndex].x = translation[0];
        Translation[faceIndex].y = translation[1];
        Translation[faceIndex].z = translation[2];

        Rotation[faceIndex].x = rotation[0];
        Rotation[faceIndex].y = rotation[1];
        Rotation[faceIndex].z = rotation[2];

        double HeadPitchCompensatedRad = Rotation[faceIndex].x - Math.Atan2(Translation[faceIndex].y, Translation[faceIndex].z);
        double HeadYawCompensatedRad = Rotation[faceIndex].y - Math.Atan2(Translation[faceIndex].x, Translation[faceIndex].z);
        double HeadRollRad = Rotation[faceIndex].z;
        
        double HeadPitchCompensatedDeg = HeadPitchCompensatedRad * Mathf.Rad2Deg;
        double HeadYawCompensatedDeg = HeadYawCompensatedRad * Mathf.Rad2Deg;
        double HeadRollDeg = HeadRollRad * Mathf.Rad2Deg;

        const double CONSTRAINT_ANGLE = 40;
        
        if (Math.Abs(HeadPitchCompensatedDeg) > CONSTRAINT_ANGLE ||
            Math.Abs(HeadYawCompensatedDeg) > CONSTRAINT_ANGLE ||
            Math.Abs(HeadRollDeg) > CONSTRAINT_ANGLE ||
            VisageTrackerNative._getFaceScale(faceIndex) < 40)
            return false;

        return true;
    }
#endif
}
