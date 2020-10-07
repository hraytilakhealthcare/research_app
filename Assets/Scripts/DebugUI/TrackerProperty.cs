using System;

public enum TrackerProperty
{
    IPD,
    DistanceOffset, //TODO: remove (probably?)
    FocalLenght, //TODO: find a way to edit it in tracker's config or remove
    FrameAnalysisDelay,
    Smoothing
}

public static class TrackerProperties
{
    public static float GetUnitModifier(TrackerProperty property)
    {
        switch (property)
        {
            case TrackerProperty.FocalLenght:
                return 1 / 10f;
            case TrackerProperty.Smoothing:
            case TrackerProperty.FrameAnalysisDelay:
            case TrackerProperty.DistanceOffset:
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
            case TrackerProperty.DistanceOffset:
                return "m";
            case TrackerProperty.FocalLenght:
                return "mm";
            case TrackerProperty.Smoothing:
            case TrackerProperty.FrameAnalysisDelay:
                return "s";
            default:
                throw new ArgumentOutOfRangeException(nameof(property), property, null);
        }
    }
}
