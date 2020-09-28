using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Slider))]
public class IPDWidget : MonoBehaviour
{
    private Slider slider;
    private Tracker tracker;

    private void Awake()
    {
        tracker = FindObjectOfType<Tracker>();
        slider = GetComponent<Slider>();
        slider.minValue = MIN;
        slider.maxValue = MAX;
        slider.onValueChanged.AddListener(OnChanged);
    }


    private const float MIN = 0.055f;
    private const float MAX = 0.075f;

    private void Update()
    {
        if (tracker.TrackStatus != TrackStatus.OK) return;
        slider.value = tracker.IPD;
    }

    private void OnChanged(float val)
    {
        tracker.IPD = val;
    }
}
