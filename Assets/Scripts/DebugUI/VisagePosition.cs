using UnityEngine;

public class VisagePosition : MonoBehaviour
{
    private Tracker tracker;
    private TextMesh textMesh;
    private Vector3 linearVelocity;
    private Vector3 currentRotation;
    private Vector3 angularVelocity;

    private void Awake()
    {
        tracker = FindObjectOfType<Tracker>();
        textMesh = GetComponentInChildren<TextMesh>();
        currentRotation = transform.rotation.eulerAngles;
    }

    private void Update()
    {
        VisageTrackerApi.HeadInfo headInfo = VisageTrackerApi.LastHeadInfo;
        MoveTo(headInfo, tracker.Smoothing);
        UpdateDistanceDisplay(headInfo);
    }

    private void MoveTo(VisageTrackerApi.HeadInfo headInfo, float smoothTime)
    {
        Vector3 position = Vector3.SmoothDamp(
            transform.position,
            headInfo.Position,
            ref linearVelocity,
            smoothTime
        );
        currentRotation = SmoothDampAsAngle(
            currentRotation,
            headInfo.Rotation,
            ref angularVelocity,
            smoothTime
        );
        transform.SetPositionAndRotation(position, Quaternion.Euler(currentRotation));
    }

    private static Vector3 SmoothDampAsAngle(Vector3 current, Vector3 target, ref Vector3 velocity, float smoothTime)
    {
        return new Vector3(
            Mathf.SmoothDampAngle(current.x, target.x, ref velocity.x, smoothTime),
            Mathf.SmoothDampAngle(current.y, target.y, ref velocity.y, smoothTime),
            Mathf.SmoothDampAngle(current.z, target.z, ref velocity.z, smoothTime)
        );
    }


    private void UpdateDistanceDisplay(VisageTrackerApi.HeadInfo headInfo)
    {
        float distance = headInfo.Position.magnitude;
        textMesh.text = $"{distance:0.00}";
        textMesh.color = IsRightRange(distance) ? Color.green : Color.red;
    }

    private static bool IsRightRange(float distance)
    {
        return distance >= .35f && distance <= .45f;
    }
}
