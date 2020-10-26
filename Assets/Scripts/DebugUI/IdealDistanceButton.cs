using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class IdealDistanceButton : MonoBehaviour
{
    [SerializeField] private float currentPixelSize;
    [SerializeField] private bool isLeftEye;
    private ButtonStep currentStep;
    private Button button;
    private Text textComponent;
    private float minIrisSize, maxIrisSize;

    private const int StepCount = 3;
    private const float MinDistance = 35;
    private const float MaxDistance = 45;
    private const float ValidRange = MaxDistance - MinDistance;

    private enum ButtonStep
    {
        SaveMax = 0,
        SaveMin = 1,
        Reset = 2
    }

    private void Awake()
    {
        button = GetComponent<Button>();
        button.onClick.AddListener(OnClick);
        textComponent = GetComponentInChildren<Text>();
    }

    private void Update()
    {
        textComponent.text = ComputeText();
    }

    private string ComputeText()
    {
        bool hasLimitsSet = minIrisSize > 0 && maxIrisSize > 0;
        return hasLimitsSet
            ? $"Press to {currentStep}\n{currentPixelSize}px({ComputeDistance()}cm)"
            : $"Press to {currentStep}\n{currentPixelSize}px";
    }

    private float ComputeDistance()
    {
        float min = Mathf.Min(minIrisSize, maxIrisSize);
        float max = Mathf.Max(minIrisSize, maxIrisSize);
        float range = max - min;
        float normalized = (currentPixelSize - min) / range;
        return normalized * ValidRange + MinDistance;
    }

    private void OnClick()
    {
        switch (currentStep)
        {
            case ButtonStep.SaveMax:
                maxIrisSize = currentPixelSize;
                break;
            case ButtonStep.SaveMin:
                minIrisSize = currentPixelSize;
                break;
            case ButtonStep.Reset:
                maxIrisSize = 0;
                minIrisSize = 0;
                break;
        }

        currentStep = (ButtonStep) ((int) (currentStep + 1) % StepCount);
    }
}
