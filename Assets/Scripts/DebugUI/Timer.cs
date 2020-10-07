using UnityEngine;

public class Timer
{
    private float currentTime;
    private float startTime;
    private float Elapsed => currentTime - startTime;
    private float duration;


    public void Update(float time)
    {
        currentTime = time;
    }

    public bool IsElapsed()
    {
        return Elapsed > duration;
    }

    public void Reset()
    {
        startTime = currentTime;
    }

    public void SetDuration(float aDuration)
    {
        duration = Mathf.Max(0, aDuration);
    }
}
