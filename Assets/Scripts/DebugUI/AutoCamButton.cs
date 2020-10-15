using UnityEngine;
using UnityEngine.UI;

namespace DefaultNamespace
{
    public class AutoCamButton : MonoBehaviour
    {
        private Button button;

        private void Awake()
        {
            button = GetComponent<Button>();
            button.onClick.AddListener(OnClicked);
        }

        private static void OnClicked()
        {
            VisageTrackerApi.Init();
            FindObjectOfType<Tracker>().ResetValues();
        }
    }
}
