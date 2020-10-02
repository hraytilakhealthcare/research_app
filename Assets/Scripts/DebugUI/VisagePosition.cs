using UnityEngine;

public class VisagePosition : MonoBehaviour
{
    private Tracker tracker;
    private TextMesh textMesh;

    private void Awake()
    {
        tracker = FindObjectOfType<Tracker>();
        textMesh = GetComponentInChildren<TextMesh>();
    }

    private void Update()
    {
        Vector3 translation = tracker.Translation;
        transform.SetPositionAndRotation(translation, Quaternion.Euler(tracker.Rotation));
        float distance = tracker.FaceDistance();
        textMesh.text = $"{distance:0.00}";
        textMesh.color = IsRightRange(distance) ? Color.green : Color.red;
    }

    private bool IsRightRange(float distance)
    {
        return distance >= .35f && distance <= .45f;
    }
}
