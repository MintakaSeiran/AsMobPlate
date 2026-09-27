using System;

namespace AsMobPlate.Windows;

internal static class PreviewAnimation
{
    public static double RemainingSeconds(double elapsed, int countdownSeconds)
    {
        var duration = Math.Clamp(countdownSeconds, 5, 30);
        if (!double.IsFinite(elapsed) || elapsed < 0)
            elapsed = 0;
        // Hold the in-progress appearance for three seconds before the next preview cycle.
        return duration - elapsed % (duration + 3);
    }
}
