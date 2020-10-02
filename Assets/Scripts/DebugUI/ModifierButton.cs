using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class ModifierButton : MonoBehaviour
{
    [SerializeField] internal TrackerProperty property;
    [SerializeField] internal int delta;
    private Tracker tracker;

    private void Start()
    {
        tracker = FindObjectOfType<Tracker>();
        Assert.IsNotNull(tracker);
        Assert.AreNotEqual(0, delta);
        GetComponent<Button>().onClick.AddListener(OnClicked);
        GetComponentInChildren<Text>().text = $"{delta:+0;-#}";
    }

    private void OnClicked()
    {
        tracker[property] += delta * TrackerProperties.GetUnitModifier(property);
    }
}
