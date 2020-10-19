using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Text))]
// ReSharper disable once InconsistentNaming
public class InfoDisplay : MonoBehaviour
{
    protected Text textComponent;
    private FpsCounter fpsCounter;

    private void Awake()
    {
        textComponent = GetComponent<Text>();
        fpsCounter = gameObject.AddComponent<FpsCounter>();
    }

    private void Update()
    {
        VisageTrackerApi.CameraInfo cameraInfo = VisageTrackerApi.LastCameraInfo;
        VisageTrackerApi.TrackerStatus status = VisageTrackerApi.Status;
        VisageTrackerApi.HeadInfo head = VisageTrackerApi.LastHeadInfo;
        textComponent.text = $"Init:{VisageTrackerApi.IsInit}\t" +
                             $"Status:{status.TrackingStatus}\n" +
                             $"Quality:{status.Quality:P0}\n" +
                             $"FPS:{fpsCounter.FramePerSecond:00}(unity){status.FrameRate:00}(plugin)\n" +
                             // $"IPD:{tracker.IPD:0.000}m\n" +
                             $"DST:{VisageTrackerApi.LastHeadInfo.Position.magnitude:00.00}m\n" +
                             $"IMG:{cameraInfo.ImageSize.x}x{cameraInfo.ImageSize.y}px\n" +
                             $"Focal:{cameraInfo.FocalLenght}cm\n" +
                             $"Eyes:(L:{head.LeftEyeOpening:00.00}, R{head.RightEyeOpening:00.00})\n";
    }
}
