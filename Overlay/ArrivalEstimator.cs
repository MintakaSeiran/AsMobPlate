using System;
using System.Numerics;

namespace AsMobPlate.Overlay;

// Fixed storage; positions are sampled once per frame, independently of hunt count.
public sealed class ArrivalEstimator
{
    private Vector3 origin;
    private Vector3 previous;
    private Vector3 velocity;
    private double started;
    private double last;
    private bool initialized;
    private bool ready;

    public void Reset() => this.initialized = this.ready = false;

    public void Update(Vector3 position, double now)
    {
        var delta = now - this.last;
        if (!this.initialized || delta <= 0 || delta > 1 ||
            Vector3.Distance(position, this.previous) > 40 * delta)
        {
            this.origin = this.previous = position;
            this.started = this.last = now;
            this.initialized = true;
            this.ready = false;
            return;
        }
        // Invalidate immediately on stopping, rather than retaining a stale arrival estimate.
        if (Vector3.Distance(position, this.previous) / delta < 0.1)
        {
            this.origin = position;
            this.started = now;
            this.ready = false;
        }
        this.previous = position;
        this.last = now;
        if (now - this.started >= 2)
        {
            this.velocity = (position - this.origin) / (float)(now - this.started);
            this.ready = true;
            this.origin = position;
            this.started = now;
        }
    }

    public double? Estimate(Vector3 position, Vector3 target, float arrivalDistance, float preparationSeconds)
    {
        var direction = target - position;
        var distance = direction.Length();
        if (!this.ready || !float.IsFinite(distance) || distance < 200)
            return null;
        var closingSpeed = Vector3.Dot(this.velocity, direction / distance);
        if (!float.IsFinite(closingSpeed) || closingSpeed < 0.5f)
            return null;
        return Math.Max(0, distance - Math.Clamp(arrivalDistance, 0, 100)) / closingSpeed
            + Math.Clamp(preparationSeconds, 0, 60);
    }
}
