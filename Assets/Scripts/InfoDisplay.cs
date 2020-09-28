using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
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
                             $"DST:{tracker.Translation.magnitude:00.00}m\n";
    }

    private static string EyesAsString(IReadOnlyList<float> eye)
    {
        Assert.AreEqual(2, eye.Count);
        return $"Left:{eye[0]:P}, Right:{eye[1]}";
    }
}
