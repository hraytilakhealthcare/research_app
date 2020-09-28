using UnityEngine;

public class FpsCounter : MonoBehaviour
{
    public double FramePerSecond { get; private set; }
    private double startTime;
    private double updateFrequency = 1;
    private int frameCount;

    private void Awake()
    {
        startTime = -updateFrequency;
    }

    private void Update()
    {
        if (TimeElapsed()) FinishWait();
        else frameCount++;
    }

    private void FinishWait()
    {
        Save();
        Reset();
    }

    private void Reset()
    {
        frameCount = 0;
        startTime = Time.unscaledTime;
    }

    private void Save()
    {
        double duration = Time.unscaledTime - startTime;
        FramePerSecond = frameCount / duration;
    }


    private bool TimeElapsed()
    {
        return Time.unscaledTime - startTime > updateFrequency;
    }
}
