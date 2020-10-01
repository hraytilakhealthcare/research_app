using UnityEngine;
using UnityEngine.UI;

namespace DefaultNamespace
{
    public class FocalModifierButton : MonoBehaviour
    {
        private Button button;
        [SerializeField] private int delta;
        private Tracker tracker;

        private void Awake()
        {
            button = GetComponent<Button>();
            button.onClick.AddListener(OnClicked);
            tracker = FindObjectOfType<Tracker>();
        }

        private void OnClicked()
        {
            float previous = tracker.CameraFocus;
            tracker.CameraFocus += delta / 10f;
        }
    }
}
