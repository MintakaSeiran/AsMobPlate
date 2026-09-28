using System;

namespace AsMobPlate.Overlay;

// A trigger alert takes priority so object detection cannot truncate its five sounds.
public sealed class NotificationSoundSequence
{
    private int remaining;
    private double nextAt;
    private bool priority;

    public void Queue(int count, double now, bool isPriority = false)
    {
        if (!isPriority && this.priority && (this.remaining > 0 || now < this.nextAt))
            return;
        this.remaining = Math.Clamp(count, 1, 20);
        this.priority = isPriority;
        this.nextAt = now;
    }

    public bool Take(double now, double interval)
    {
        if (this.remaining == 0 || now < this.nextAt)
            return false;
        this.remaining--;
        this.nextAt = now + (double.IsFinite(interval) ? Math.Clamp(interval, 0.05, 2) : 0.25);
        return true;
    }

    public void Clear()
    {
        this.remaining = 0;
        this.priority = false;
        this.nextAt = 0;
    }
}
