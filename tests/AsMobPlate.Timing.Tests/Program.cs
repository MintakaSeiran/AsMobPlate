using AsMobPlate.Overlay;

var passed = 0;
var failed = 0;

Run("One ET minute is 35/12 real seconds", () =>
{
    var schedule = Create(At(13, 13), 13, 14);
    Near(2.916666666666667, schedule.GetRemainingSeconds(At(13, 13)));
});
Run("One ET hour is 175 real seconds", () =>
{
    var schedule = Create(At(13, 13), 14, 13);
    Near(175, schedule.GetRemainingSeconds(At(13, 13)));
});
Run("13:13 received stays 13:13 and 13:19 is 17.5 seconds away", () =>
{
    var schedule = Create(At(13, 13), 13, 19);
    Equal("13:13", EorzeaStartSchedule.FormatTime(schedule.AnnouncedAt));
    Equal("13:19", EorzeaStartSchedule.FormatTime(schedule.Target));
    Near(17.5, schedule.GetRemainingSeconds(At(13, 13)));
});
Run("ET seconds are retained instead of flooring to the minute", () =>
{
    var schedule = Create(At(13, 13, 30), 13, 19);
    Near(16.041666666666668, schedule.GetRemainingSeconds(At(13, 13, 30)));
});
Run("The last ET second before the target is still waiting", () =>
{
    var schedule = Create(At(13, 13, 59), 13, 14);
    Near(0.04861111111111111, schedule.GetRemainingSeconds(At(13, 13, 59)));
    Equal(1, (int)Math.Ceiling(schedule.GetRemainingSeconds(At(13, 13, 59))));
});
Run("At the target ET the remaining time is exactly zero", () =>
{
    var schedule = Create(At(13, 13), 13, 19);
    Near(0, schedule.GetRemainingSeconds(At(13, 19)));
});
Run("Remaining time follows advances in the game clock", () =>
{
    var schedule = Create(At(13, 13), 13, 19);
    Near(10.5, schedule.GetRemainingSeconds(At(13, 13) + 144));
});
Run("Future midnight uses the next ET day", () =>
{
    var schedule = Create(At(23, 59), 0, 1);
    Equal(At(0, 1) + 86400, schedule.Target);
    Near(5.833333333333333, schedule.GetRemainingSeconds(At(23, 59)));
});
Run("Recent midnight uses the previous ET day", () =>
{
    var schedule = Create(At(0, 1), 23, 59);
    Equal(At(23, 59) - 86400, schedule.Target);
    Near(-5.833333333333333, schedule.GetRemainingSeconds(At(0, 1)));
    Equal(true, schedule.CountdownHandled);
});
Run("A recent start is negative, never tomorrow", () =>
{
    var schedule = Create(At(13, 19), 13, 13);
    Near(-17.5, schedule.GetRemainingSeconds(At(13, 19)));
});
Run("Same ET minute retains elapsed seconds", () =>
{
    var schedule = Create(At(13, 13, 30), 13, 13);
    Near(-1.4583333333333333, schedule.GetRemainingSeconds(At(13, 13, 30)));
});
Run("Both two-hour boundaries are included", () =>
{
    var future = Create(At(12, 0), 14, 0);
    var past = Create(At(12, 0), 10, 0);
    Near(350, future.GetRemainingSeconds(At(12, 0)));
    Near(-350, past.GetRemainingSeconds(At(12, 0)));
});
Run("Times outside the two-hour window do not schedule a next-day wait", () =>
{
    var schedule = new EorzeaStartSchedule();
    Equal(AnnouncementResult.OutsideWindow, schedule.Set(At(12, 0), 14 * 60 + 1, 120, 10));
    Equal(AnnouncementResult.OutsideWindow, schedule.Set(At(12, 0, 1), 10 * 60, 120, 10));
    Equal(AnnouncementResult.OutsideWindow, schedule.Set(At(12, 0), 0, 120, 10));
    Equal(false, schedule.HasAnnouncement);
});
Run("Invalid game times, times of day and windows are rejected", () =>
{
    var schedule = new EorzeaStartSchedule();
    Equal(AnnouncementResult.OutsideWindow, schedule.Set(0, 1, 120, 10));
    Equal(AnnouncementResult.OutsideWindow, schedule.Set(At(12, 0), -1, 120, 10));
    Equal(AnnouncementResult.OutsideWindow, schedule.Set(At(12, 0), 1440, 120, 10));
    Equal(AnnouncementResult.OutsideWindow, schedule.Set(At(12, 0), 720, float.NaN, 10));
});
Run("An invalid announcement preserves the active target", () =>
{
    var schedule = Create(At(13, 13), 13, 19);
    Equal(AnnouncementResult.OutsideWindow, schedule.Set(At(13, 14), 0, 120, 10));
    Equal(At(13, 19), schedule.Target);
});
Run("A repeated announcement preserves the original receipt ET", () =>
{
    var schedule = Create(At(13, 13), 13, 19);
    Equal(AnnouncementResult.Duplicate, schedule.Set(At(13, 14), 13 * 60 + 19, 120, 20));
    Equal(At(13, 13), schedule.AnnouncedAt);
    Equal(10, schedule.CountdownSeconds);
});
Run("No early countdown, and exactly one attempt when crossing ten seconds", () =>
{
    var schedule = Create(At(13, 13), 13, 19);
    Equal(CountdownDecision.None, schedule.TakeCountdown(schedule.Target - 206));
    Equal(CountdownDecision.Start, schedule.TakeCountdown(schedule.Target - 205));
    Equal(CountdownDecision.None, schedule.TakeCountdown(schedule.Target - 204));
});
Run("A duplicate does not rearm an already attempted countdown", () =>
{
    var schedule = Create(At(13, 13), 13, 19);
    Equal(CountdownDecision.Start, schedule.TakeCountdown(schedule.Target - 205));
    Equal(AnnouncementResult.Duplicate, schedule.Set(schedule.Target - 204, 13 * 60 + 19, 120, 10));
    Equal(CountdownDecision.None, schedule.TakeCountdown(schedule.Target - 204));
});
Run("An announcement with less than ten seconds does not start a late full countdown", () =>
{
    var schedule = Create(At(13, 16), 13, 19);
    Equal(true, schedule.CountdownHandled);
    Equal(CountdownDecision.None, schedule.TakeCountdown(At(13, 16)));
});
Run("A paused framework that misses the trigger does not send a late countdown", () =>
{
    var schedule = Create(At(13, 13), 13, 19);
    Equal(CountdownDecision.SkippedLate, schedule.TakeCountdown(schedule.Target - 180));
    Equal(CountdownDecision.None, schedule.TakeCountdown(schedule.Target - 179));
});
Run("The countdown is never sent once the start has passed", () =>
{
    var schedule = Create(At(13, 13), 13, 19);
    Equal(CountdownDecision.SkippedLate, schedule.TakeCountdown(schedule.Target));
    Equal(CountdownDecision.None, schedule.TakeCountdown(schedule.Target + 1));
});
Run("A corrected target replaces the old schedule and arms once", () =>
{
    var schedule = Create(At(13, 13), 13, 19);
    Equal(CountdownDecision.Start, schedule.TakeCountdown(schedule.Target - 205));
    Equal(AnnouncementResult.Accepted, schedule.Set(At(13, 16), 13 * 60 + 25, 120, 10));
    Equal(false, schedule.CountdownHandled);
    Equal(CountdownDecision.Start, schedule.TakeCountdown(schedule.Target - 205));
});
Run("Clearing a schedule cancels pending countdowns", () =>
{
    var schedule = Create(At(13, 13), 13, 19);
    schedule.Clear();
    Equal(false, schedule.HasAnnouncement);
    Equal(CountdownDecision.None, schedule.TakeCountdown(At(13, 16)));
});
Run("Configured countdown length defines the trigger as well", () =>
{
    var schedule = new EorzeaStartSchedule();
    Equal(AnnouncementResult.Accepted, schedule.Set(At(12, 0), 12 * 60 + 20, 120, 20));
    Equal(20, schedule.CountdownSeconds);
    Equal(CountdownDecision.None, schedule.TakeCountdown(schedule.Target - 412));
    Equal(CountdownDecision.Start, schedule.TakeCountdown(schedule.Target - 411));
});
Run("Midnight simulation sends once, at most 0.05 seconds after the ten-second point", () =>
{
    var schedule = Create(At(23, 55), 0, 1);
    var attempts = 0;
    for (var time = schedule.AnnouncedAt; time <= schedule.Target + 60; time++)
    {
        if (schedule.TakeCountdown(time) != CountdownDecision.Start)
            continue;
        attempts++;
        var remaining = schedule.GetRemainingSeconds(time);
        if (remaining > 10 || remaining < 9.95)
            throw new Exception($"Unexpected trigger: {remaining}");
    }
    Equal(1, attempts);
});
Run("Display formatting does not apply a local time zone or clock offset", () =>
{
    Equal("13:13", EorzeaStartSchedule.FormatTime(At(13, 13, 59)));
    Equal("13:13:59", EorzeaStartSchedule.FormatTime(At(13, 13, 59), true));
    Equal("00:00:00", EorzeaStartSchedule.FormatTime(At(0, 0), true));
});

Console.WriteLine($"{passed} passed, {failed} failed");
return failed == 0 ? 0 : 1;

void Run(string name, Action test)
{
    try
    {
        test();
        passed++;
        Console.WriteLine($"PASS {name}");
    }
    catch (Exception ex)
    {
        failed++;
        Console.Error.WriteLine($"FAIL {name}: {ex.Message}");
    }
}

static long At(int hour, int minute, int second = 0) => 400000L * 86400 + hour * 3600 + minute * 60 + second;

static EorzeaStartSchedule Create(long received, int targetHour, int targetMinute)
{
    var schedule = new EorzeaStartSchedule();
    Equal(AnnouncementResult.Accepted, schedule.Set(received, targetHour * 60 + targetMinute, 120, 10));
    return schedule;
}

static void Equal<T>(T expected, T actual)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new Exception($"Expected {expected}, got {actual}");
}

static void Near(double expected, double actual)
{
    if (!double.IsFinite(actual) || Math.Abs(expected - actual) > 0.00000001)
        throw new Exception($"Expected {expected}, got {actual}");
}
