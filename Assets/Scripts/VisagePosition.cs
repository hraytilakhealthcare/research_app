using UnityEngine;

public class VisagePosition : MonoBehaviour
{
    private Tracker tracker;

    private void Awake()
    {
        tracker = FindObjectOfType<Tracker>();
    }

    private void Update()
    {
        transform.SetPositionAndRotation(tracker.Translation, Quaternion.Euler(tracker.Rotation));
    }
}
