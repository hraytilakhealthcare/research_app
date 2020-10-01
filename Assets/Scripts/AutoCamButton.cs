using UnityEngine;
using UnityEngine.UI;

namespace DefaultNamespace
{
    public class AutoCamButton : MonoBehaviour
    {
        private Button button;
        private Tracker tracker;

        private void Awake()
        {
            button = GetComponent<Button>();
            button.onClick.AddListener(OnClicked);
            tracker = FindObjectOfType<Tracker>();
        }

        private void OnClicked()
        {
            tracker.AutoConfigureCamera();
        }
    }
}
