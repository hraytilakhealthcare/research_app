using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class EyeDisplay : MonoBehaviour
{
    [SerializeField] private bool isLeftEye;
    [SerializeField] private Sprite openSprite;
    [SerializeField] private Sprite closedSprite;
    private Image image;

    private void Awake()
    {
        image = GetComponent<Image>();
        string eye = isLeftEye ? "left" : "right";
        name = $"eye:{eye}";
    }

    private void Update()
    {
        image.color = IsEyeOpen() ? Color.green : Color.red;
        image.overrideSprite = IsEyeOpen() ? openSprite : closedSprite;
    }

    private bool IsEyeOpen()
    {
        return isLeftEye
            ? VisageTrackerApi.LastHeadInfo.LeftEyeOpening
            : VisageTrackerApi.LastHeadInfo.RightEyeOpening;
    }
}
