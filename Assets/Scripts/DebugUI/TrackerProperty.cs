using System;

public enum TrackerProperty
{
    IPD,
    FrameAnalysisDelay,
    Smoothing
}

public static class TrackerProperties
{
    public static float GetUnitModifier(TrackerProperty property)
    {
        switch (property)
        {
            case TrackerProperty.Smoothing:
            case TrackerProperty.FrameAnalysisDelay:
                return 1 / 100f;
            case TrackerProperty.IPD:
                return 1 / 1000f;
            default:
                throw new ArgumentOutOfRangeException(nameof(property), property, null);
        }
    }

    public static string GetUnitName(TrackerProperty property)
    {
        switch (property)
        {
            case TrackerProperty.IPD:
            case TrackerProperty.Smoothing:
            case TrackerProperty.FrameAnalysisDelay:
                return "s";
            default:
                throw new ArgumentOutOfRangeException(nameof(property), property, null);
        }
    }
}
