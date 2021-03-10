using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DebugUI
{
    [RequireComponent(typeof(Button))]
    public class ToggleObjectButton : MonoBehaviour
    {
        [SerializeField] private List<GameObject> targets;
        private Button button;
        private Text content;

        private void Awake()
        {
            button = GetComponent<Button>();
            content = GetComponentInChildren<Text>();
            button.onClick.AddListener(Click);
        }

        private void Update()
        {
            if (!VisageTrackerApi.IsInit) return;
            content.text = $"DST:{VisageTrackerApi.LastHeadInfo.Position.magnitude:00.00}m";
        }

        private void Click()
        {
            foreach (GameObject target in targets) target.SetActive(!target.activeSelf);
        }
    }
}
