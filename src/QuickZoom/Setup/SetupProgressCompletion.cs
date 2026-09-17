namespace QuickZoom;

internal readonly record struct SetupProgressFrame(float Progress, float SuccessBlend, bool IsFinished);

internal static class SetupProgressCompletion
{
    internal const int PauseMilliseconds = 150;
    internal const int FillMilliseconds = 650;
    internal const int GreenMilliseconds = 220;
    internal const int HoldMilliseconds = 220;
    internal const int DurationMilliseconds = PauseMilliseconds + FillMilliseconds + GreenMilliseconds + HoldMilliseconds;

    // Called only after startup verification has succeeded. Keep the status busy
    // until the full bar has turned green and remained visible for a short beat.
    internal static SetupProgressFrame GetFrame(float startProgress, long elapsedMilliseconds)
    {
        float fill = Ease((elapsedMilliseconds - PauseMilliseconds) / (float)FillMilliseconds);
        float green = Ease((elapsedMilliseconds - PauseMilliseconds - FillMilliseconds) / (float)GreenMilliseconds);
        float start = Math.Clamp(startProgress, 0f, 1f);
        return new(start + (1f - start) * fill, green, elapsedMilliseconds >= DurationMilliseconds);
    }

    private static float Ease(float value)
    {
        float t = Math.Clamp(value, 0f, 1f);
        return t * t * (3f - 2f * t);
    }
}
