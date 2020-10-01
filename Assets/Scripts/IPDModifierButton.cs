using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class IPDModifierButton : MonoBehaviour
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
        tracker.IPD += delta / 1000f;
    }
}
