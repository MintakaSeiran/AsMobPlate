using System;
using System.Collections.Generic;
using System.Numerics;

namespace AsMobPlate.Hunts;

public enum HuntStage { None, Searching, Fighting, Boss, Defeated, Returned }

// Owns copied observations only: disappearing objects never imply death.
public sealed class HuntProgress
{
    public sealed class Observation
    {
        public ulong Id;
        public uint NameId;
        public uint ObjectIndex;
        public string Name = string.Empty;
        public HuntRank Rank;
        public Vector3 Position;
        public float Radius;
        public double LastSeen;
        public double? DiedAt;
    }

    private readonly Dictionary<ulong, Observation> observations = new();
    private readonly List<ulong> expired = new();
    private readonly HashSet<ulong> defeatedMinions = new();
    public Dictionary<ulong, Observation>.ValueCollection Observations => this.observations.Values;
    public HuntStage Stage { get; private set; }
    public double? StartedAt { get; private set; }
    public double ChangedAt { get; private set; }
    public int ObservedKills => this.defeatedMinions.Count;
    public double LastActivity { get; private set; }

    public void Clear()
    {
        this.observations.Clear();
        this.defeatedMinions.Clear();
        this.Stage = HuntStage.None;
        this.StartedAt = null;
        this.LastActivity = 0;
    }

    public void Start(double now)
    {
        // Duplicate delivery must not reset the deadline or observed kills.
        if (this.StartedAt is double start && now - start < 5)
            return;
        this.defeatedMinions.Clear();
        this.StartedAt = now;
        this.SetStage(HuntStage.Searching, now);
    }

    public void Return(double now)
    {
        if (this.Stage is HuntStage.Searching or HuntStage.Fighting)
            this.SetStage(HuntStage.Returned, now);
    }

    public Observation? Find(ulong id) => this.observations.GetValueOrDefault(id);

    public void Observe(ulong id, uint nameId, uint index, string name, HuntRank rank,
        Vector3 position, float radius, bool dead, bool fighting, bool eventEligible, double now)
    {
        if (!this.observations.TryGetValue(id, out var item) || item.NameId != nameId)
        {
            if (this.observations.Count >= 256)
                return;
            item = new Observation { Id = id, NameId = nameId, ObjectIndex = index, Name = name, Rank = rank };
            this.observations[id] = item;
        }
        item.LastSeen = now;
        // Preserve the last living position when the client removes a corpse.
        if (item.DiedAt == null)
        {
            item.Position = position;
            item.Radius = radius;
        }
        var newlyDead = dead && item.DiedAt == null;
        if (newlyDead)
            item.DiedAt = now;
        if (!dead && item.DiedAt != null)
            return;
        if (!eventEligible)
            return;
        if (rank == HuntRank.SS)
        {
            if (newlyDead)
                this.SetStage(HuntStage.Defeated, now);
            else if (!dead && this.Stage != HuntStage.Defeated)
                this.SetStage(HuntStage.Boss, now);
        }
        else if (rank == HuntRank.Minion && this.Stage is not (HuntStage.Boss or HuntStage.Defeated or HuntStage.Returned))
        {
            if (this.Stage == HuntStage.None && !dead)
                this.SetStage(HuntStage.Searching, now);
            if (this.Stage != HuntStage.None)
            {
                if (newlyDead)
                    this.defeatedMinions.Add(id);
                if (fighting && !dead)
                    this.SetStage(HuntStage.Fighting, now);
                this.LastActivity = now;
            }
        }
    }

    public void Prune(double now, double deadDuration)
    {
        this.expired.Clear();
        foreach (var (id, item) in this.observations)
            if (now - item.LastSeen > Math.Max(30, deadDuration))
                this.expired.Add(id);
        foreach (var id in this.expired)
            this.observations.Remove(id);
        // No failure inference: an abandoned observation session simply expires.
        if (this.Stage != HuntStage.None && now - this.LastActivity > 1800)
            this.Clear();
    }

    private void SetStage(HuntStage stage, double now)
    {
        if (this.Stage != stage)
        {
            this.Stage = stage;
            this.ChangedAt = now;
        }
        this.LastActivity = now;
    }
}
