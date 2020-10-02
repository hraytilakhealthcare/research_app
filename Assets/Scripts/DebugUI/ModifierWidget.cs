using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.UI;

namespace DefaultNamespace
{
    public class ModifierWidget : MonoBehaviour
    {
        [SerializeField] private TrackerProperty property;
        private Text title;
        private Tracker tracker;

        private void Awake()
        {
            List<Button> buttons = GetComponentsInChildren<Button>().ToList();
            Assert.AreEqual(2, buttons.Count);
            buttons.Sort((b1, b2) => b1.transform.position.x.CompareTo(b2.transform.position.x));

            ModifierButton plusButton = buttons[0].gameObject.AddComponent<ModifierButton>();
            plusButton.property = property;
            plusButton.delta = 1;

            ModifierButton minusButton = buttons[1].gameObject.AddComponent<ModifierButton>();
            minusButton.property = property;
            minusButton.delta = -1;
            title = GetComponentInChildren<Text>();

            tracker = FindObjectOfType<Tracker>();
            name = $"{property} widget";
        }

        private void Update()
        {
            title.text = $"{property}:{tracker[property]:0.000}{TrackerProperties.GetUnitName(property)}";
        }
    }
}
