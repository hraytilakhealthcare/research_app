using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Text))]
// ReSharper disable once InconsistentNaming
public class InfoDisplay : MonoBehaviour
{
    protected Text textComponent;
    private FpsCounter fpsCounter;
    private Tracker tracker;

    private void Awake()
    {
        textComponent = GetComponent<Text>();
        fpsCounter = gameObject.AddComponent<FpsCounter>();
        tracker = FindObjectOfType<Tracker>();
    }

    private void Update()
    {
        VisageTrackerApi.CameraInfo cameraInfo = VisageTrackerApi.LastCameraInfo;
        VisageTrackerApi.TrackerStatus status = VisageTrackerApi.Status;
        VisageTrackerApi.HeadInfo head = VisageTrackerApi.LastHeadInfo;
        Debug.Log($"({head.LeftEyeOpening},{head.RightEyeOpening})");
        textComponent.text = $"Init:{VisageTrackerApi.IsInit}\t" +
                             $"Status:{status.TrackingStatus}\n" +
                             $"Quality:{status.Quality:P0}\n" +
                             $"FPS:{fpsCounter.FramePerSecond:00}(unity){status.FrameRate:00}(plugin)\n" +
                             $"IPD:{tracker.IPD:0.000}m\n" +
                             $"DST:{VisageTrackerApi.LastHeadInfo.Position.magnitude:00.00}m\n" +
                             $"IMG:{cameraInfo.ImageSize.x}x{cameraInfo.ImageSize.y}px\n" +
                             $"IrisRadius:{head.IrisRadiusLeft:F1}px, {head.IrisRadiusRight:F1}px\n" +
                             $"Focal:{cameraInfo.FocalLenght}mm";
    }
}
