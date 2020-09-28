using System;
using UnityEngine;
using UnityEngine.UI;

namespace DefaultNamespace
{
    [RequireComponent(typeof(Image))]
    public class StatusDisplay : MonoBehaviour
    {
        private Image image;
        private Tracker tracker;

        private void Awake()
        {
            tracker = FindObjectOfType<Tracker>();
            image = GetComponent<Image>();
        }

        private void Update()
        {
            image.color = ColorFromStatus(tracker.TrackStatus);
        }

        private Color ColorFromStatus(TrackStatus trackerTrackStatus)
        {
            switch (tracker.TrackStatus)
            {
                case TrackStatus.OFF:
                    return Color.red;
                case TrackStatus.OK:
                    return Color.green;
                case TrackStatus.RECOVERING:
                case TrackStatus.INIT:
                    return Color.yellow;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}
