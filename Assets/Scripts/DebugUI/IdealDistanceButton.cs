using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class IdealDistanceButton : MonoBehaviour
{
    private float CurrentPixelSize =>
        isLeftEye
            ? VisageTrackerApi.LastHeadInfo.IrisRadiusLeft
            : VisageTrackerApi.LastHeadInfo.IrisRadiusRight;

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
        button.image.color = ComputeColor();
    }

    private string ComputeText()
    {
        return HasLimitsSet()
            ? $"Press to {currentStep}\nsize:{CurrentPixelSize:F1}px(dst:{ComputeDistance():F1}cm)"
            : $"Press to {currentStep}\nsize:{CurrentPixelSize:F1}px";
    }

    private Color ComputeColor()
    {
        if (!HasLimitsSet())
            return Color.gray;

        return IsAtRange() ? Color.green : Color.red;
    }

    private bool IsAtRange()
    {
        float distance = ComputeDistance();
        bool isAtRange = distance >= MinDistance && distance <= MaxDistance;
        return isAtRange;
    }

    private bool HasLimitsSet()
    {
        return minIrisSize > 0 && maxIrisSize > 0;
    }

    private float ComputeDistance()
    {
        float min = Mathf.Min(minIrisSize, maxIrisSize);
        float max = Mathf.Max(minIrisSize, maxIrisSize);
        float range = max - min;
        float normalized = (CurrentPixelSize - min) / range;
        return normalized * ValidRange + MinDistance;
    }

    private void OnClick()
    {
        switch (currentStep)
        {
            case ButtonStep.SaveMax:
                maxIrisSize = CurrentPixelSize;
                break;
            case ButtonStep.SaveMin:
                minIrisSize = CurrentPixelSize;
                break;
            case ButtonStep.Reset:
                maxIrisSize = 0;
                minIrisSize = 0;
                break;
        }

        currentStep = (ButtonStep) ((int) (currentStep + 1) % StepCount);
    }
}
