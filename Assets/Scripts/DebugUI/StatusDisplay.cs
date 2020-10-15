using System;
using UnityEngine;
using UnityEngine.UI;

namespace DefaultNamespace
{
    [RequireComponent(typeof(Image))]
    public class StatusDisplay : MonoBehaviour
    {
        private Image image;

        private void Awake()
        {
            image = GetComponent<Image>();
        }

        private void Update()
        {
            image.color = ColorFromStatus(VisageTrackerApi.Status.TrackingStatus);
        }

        private Color ColorFromStatus(VisageTrackerApi.TrackStatus status)
        {
            switch (status)
            {
                case VisageTrackerApi.TrackStatus.Off:
                    return Color.red;
                case VisageTrackerApi.TrackStatus.Ok:
                    return Color.green;
                case VisageTrackerApi.TrackStatus.Recovering:
                case VisageTrackerApi.TrackStatus.Init:
                    return Color.yellow;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}
