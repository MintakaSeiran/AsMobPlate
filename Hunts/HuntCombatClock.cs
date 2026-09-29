using System;

namespace AsMobPlate.Hunts;

public sealed class HuntCombatClock
{
    private bool observedUndamaged;
    private double lastSample = double.NegativeInfinity;
    public bool InProgress { get; private set; }
    public double? StartedAt { get; private set; }

    // Integer arithmetic keeps the 99.99% boundary exact even for large HP pools.
    public static bool IsDamaged(uint hp, uint maxHp) => maxHp > 0 && hp > 0
        && (ulong)hp * 10000 <= (ulong)maxHp * 9999;

    public void Update(uint hp, uint maxHp, bool fighting, double now)
    {
        if (maxHp == 0 || hp == 0) return;
        if (now - this.lastSample > 1 || now < this.lastSample)
            this.observedUndamaged = false;
        if (hp >= maxHp && !fighting)
        {
            this.InProgress = false;
            this.StartedAt = null;
        }
        if (IsDamaged(hp, maxHp) && !this.InProgress)
        {
            this.InProgress = true;
            this.StartedAt = this.observedUndamaged && now - this.lastSample is >= 0 and <= 1 ? now : null;
        }
        this.observedUndamaged |= hp >= maxHp;
        this.lastSample = now;
    }
}
