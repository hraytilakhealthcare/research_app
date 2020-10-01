using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Text))]
// ReSharper disable once InconsistentNaming
public class InfoDisplay : MonoBehaviour
{
    public Tracker tracker;
    protected Text textComponent;
    private FpsCounter fpsCounter;

    private void Awake()
    {
        textComponent = GetComponent<Text>();
        fpsCounter = gameObject.AddComponent<FpsCounter>();
    }

    private void Update()
    {
        textComponent.text = $"Init:{tracker.IsInit}\t" +
                             $"Status:{tracker.TrackStatus}\n" +
                             $"Quality:{tracker.Quality:P0}\n" +
                             $"FPS:{fpsCounter.FramePerSecond:00}(unity){tracker.FPS:00}(plugin)\n" +
                             $"IPD:{tracker.IPD:0.000}m\n" +
                             $"DST:{tracker.Translation.magnitude:00.00}m\n" +
                             $"IMG:{tracker.ImageWidth}x{tracker.ImageHeight}px\n" +
                             $"Focal:{tracker.CameraFocus:00.00}cm\n";
    }
}
