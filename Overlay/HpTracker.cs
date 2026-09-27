using System;
using System.Collections.Generic;
using Dalamud.Game.ClientState.Objects.Types;

namespace AsMobPlate.Overlay;

public sealed class HpTracker
{
    private readonly Dictionary<ulong, HpSample> samples = new();
    private readonly HashSet<ulong> seenThisFrame = new();
    private readonly List<ulong> staleKeys = new();

    public void BeginFrame()
    {
        this.seenThisFrame.Clear();
    }

    public TimeSpan? Update(IBattleNpc npc, float sampleWindowSeconds)
    {
        var key = GetObjectKey(npc);
        this.seenThisFrame.Add(key);

        var now = DateTime.UtcNow;
        var hp = npc.CurrentHp;

        if (!this.samples.TryGetValue(key, out var sample) ||
            hp > sample.FirstHp ||
            (now - sample.FirstSeenAt).TotalSeconds > Math.Max(5.0f, sampleWindowSeconds))
        {
            this.samples[key] = new HpSample(hp, hp, now, now);
            return null;
        }

        sample.LastHp = hp;
        sample.LastSeenAt = now;
        this.samples[key] = sample;

        var elapsed = (sample.LastSeenAt - sample.FirstSeenAt).TotalSeconds;
        var lostHp = sample.FirstHp - sample.LastHp;
        if (elapsed < 3.0 || lostHp <= 0)
            return null;

        var hpPerSecond = lostHp / elapsed;
        if (hpPerSecond <= 0.0)
            return null;

        return TimeSpan.FromSeconds(sample.LastHp / hpPerSecond);
    }

    public void EndFrame()
    {
        this.staleKeys.Clear();
        foreach (var pair in this.samples)
        {
            if (!this.seenThisFrame.Contains(pair.Key))
                this.staleKeys.Add(pair.Key);
        }

        for (var i = 0; i < this.staleKeys.Count; i++)
            this.samples.Remove(this.staleKeys[i]);
    }

    private static ulong GetObjectKey(IBattleNpc npc)
    {
        return npc.GameObjectId != 0 ? npc.GameObjectId : ((ulong)npc.NameId << 32) | npc.ObjectIndex;
    }

    private struct HpSample
    {
        public ulong FirstHp;
        public ulong LastHp;
        public DateTime FirstSeenAt;
        public DateTime LastSeenAt;

        public HpSample(ulong firstHp, ulong lastHp, DateTime firstSeenAt, DateTime lastSeenAt)
        {
            this.FirstHp = firstHp;
            this.LastHp = lastHp;
            this.FirstSeenAt = firstSeenAt;
            this.LastSeenAt = lastSeenAt;
        }
    }
}
