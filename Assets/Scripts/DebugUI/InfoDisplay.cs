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
        textComponent.text = $"Init:{VisageTrackerApi.IsInit}\t" +
                             $"Status:{status.TrackingStatus}\n" +
                             $"Quality:{status.Quality:P0}\n" +
                             $"FPS:{fpsCounter.FramePerSecond:00}(unity){status.FrameRate:00}(plugin)\n" +
                             // $"IPD:{tracker.IPD:0.000}m\n" +
                             $"DST:{VisageTrackerApi.LastHeadTransform.Position.magnitude:00.00}m\n" +
                             $"IMG:{cameraInfo.ImageSize.x}x{cameraInfo.ImageSize.y}px\n" +
                             $"Focal:{cameraInfo.FocalLenght:00.00}cm\n";
    }
}
