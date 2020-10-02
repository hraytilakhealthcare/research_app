using System;

public enum TrackerProperty
{
    IPD,
    DistanceOffset,
    FocalLenght
}

public static class TrackerProperties
{
    public static float GetUnitModifier(TrackerProperty property)
    {
        switch (property)
        {
            case TrackerProperty.IPD:
                return 1 / 1000f;
            case TrackerProperty.DistanceOffset:
                return 1 / 100f;
            case TrackerProperty.FocalLenght:
                return 1 / 10f;
            default:
                throw new ArgumentOutOfRangeException(nameof(property), property, null);
        }
    }

    public static string GetUnitName(TrackerProperty property)
    {
        switch (property)
        {
            case TrackerProperty.IPD:
                return "m";
            case TrackerProperty.DistanceOffset:
                return "m";
            case TrackerProperty.FocalLenght:
                return "mm";
            default:
                throw new ArgumentOutOfRangeException(nameof(property), property, null);
        }
    }
}
