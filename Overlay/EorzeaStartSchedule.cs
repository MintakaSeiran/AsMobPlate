using System;

namespace AsMobPlate.Overlay;

// Pure scheduling logic; game clock reads and native command dispatch live in the tracker.
internal sealed class EorzeaStartSchedule
{
    public const long SecondsPerDay = 86400;
    public const double RealSecondsPerEorzeaSecond = 7.0 / 144.0;

    public bool HasAnnouncement { get; private set; }
    public long AnnouncedAt { get; private set; }
    public long Target { get; private set; }
    public int CountdownSeconds { get; private set; }
    public bool CountdownHandled { get; private set; }

    public AnnouncementResult Set(long currentEt, int targetMinute, float windowMinutes, int countdownSeconds)
    {
        if (currentEt <= 0 || targetMinute < 0 || targetMinute >= 1440 || !float.IsFinite(windowMinutes))
            return AnnouncementResult.OutsideWindow;

        var target = currentEt - currentEt % SecondsPerDay + targetMinute * 60;
        var delta = target - currentEt;
        // Resolve midnight in either direction before applying the nearby-hunt window.
        if (delta > SecondsPerDay / 2)
            target -= SecondsPerDay;
        else if (delta < -SecondsPerDay / 2)
            target += SecondsPerDay;

        if (Math.Abs(target - currentEt) > Math.Clamp(windowMinutes, 0.0f, 240.0f) * 60.0)
            return AnnouncementResult.OutsideWindow;

        if (this.HasAnnouncement && this.Target == target)
            return AnnouncementResult.Duplicate;

        this.HasAnnouncement = true;
        this.AnnouncedAt = currentEt;
        this.Target = target;
        this.CountdownSeconds = Math.Clamp(countdownSeconds, 5, 30);
        this.CountdownHandled = this.GetRemainingSeconds(currentEt) < this.CountdownSeconds;
        return AnnouncementResult.Accepted;
    }

    public double GetRemainingSeconds(long currentEt)
    {
        return (this.Target - currentEt) * RealSecondsPerEorzeaSecond;
    }

    public CountdownDecision TakeCountdown(long currentEt)
    {
        if (!this.HasAnnouncement || this.CountdownHandled)
            return CountdownDecision.None;

        var remaining = this.GetRemainingSeconds(currentEt);
        if (remaining > this.CountdownSeconds)
            return CountdownDecision.None;

        // Consume the attempt before native dispatch, including failures, to avoid retrying every frame.
        this.CountdownHandled = true;
        return remaining > 0.0 && remaining >= this.CountdownSeconds - 0.5
            ? CountdownDecision.Start
            : CountdownDecision.SkippedLate;
    }

    public void Clear()
    {
        this.HasAnnouncement = false;
        this.AnnouncedAt = 0;
        this.Target = 0;
        this.CountdownSeconds = 0;
        this.CountdownHandled = false;
    }

    public static string FormatTime(long eorzeaSeconds, bool includeSeconds = false)
    {
        var secondsOfDay = ((eorzeaSeconds % SecondsPerDay) + SecondsPerDay) % SecondsPerDay;
        var hour = secondsOfDay / 3600;
        var minute = secondsOfDay / 60 % 60;
        return includeSeconds
            ? $"{hour:00}:{minute:00}:{secondsOfDay % 60:00}"
            : $"{hour:00}:{minute:00}";
    }
}

internal enum AnnouncementResult
{
    Accepted,
    Duplicate,
    OutsideWindow,
}

internal enum CountdownDecision
{
    None,
    Start,
    SkippedLate,
}
