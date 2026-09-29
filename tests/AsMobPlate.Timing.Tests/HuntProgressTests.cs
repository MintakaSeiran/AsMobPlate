using AsMobPlate.Hunts;
using System.Numerics;

internal static class HuntProgressTests
{
    public static void Run(Action<string, Action> run)
    {
        run("HP threshold and observed combat start", () =>
        {
            Check(!HuntCombatClock.IsDamaged(99991, 100000));
            Check(HuntCombatClock.IsDamaged(99990, 100000));
            Check(!HuntCombatClock.IsDamaged(uint.MaxValue, uint.MaxValue));
            var c = new HuntCombatClock();
            c.Update(100000, 100000, false, 10);
            c.Update(99999, 100000, true, 10.1);
            c.Update(99990, 100000, true, 10.2);
            Check(c.InProgress && c.StartedAt == 10.2);
            c.Update(50000, 100000, true, 20);
            Check(c.StartedAt == 10.2);
            c.Update(100000, 100000, false, 21);
            Check(!c.InProgress && c.StartedAt == null);
        });
        run("Already damaged hunts and missed starts omit elapsed time", () =>
        {
            var c = new HuntCombatClock();
            c.Update(500, 1000, true, 10);
            Check(c.InProgress && c.StartedAt == null);
            c.Update(400, 1000, true, 11);
            Check(c.StartedAt == null);
            c = new HuntCombatClock();
            c.Update(1000, 1000, false, 10);
            c.Update(400, 1000, true, 20);
            Check(c.InProgress && c.StartedAt == null);
        });
        run("SS start and return messages match all four official languages", () =>
        {
            var messages = new AsMobPlate.Overlay.SsSystemMessages();
            string[] starts = [
                "特殊なリスキーモブの配下が、偵察活動を開始したようだ……",
                "The minions of an extraordinarily powerful mark are on the hunt for prey...",
                "Die Helfer eines besonderen Hochwilds beginnen ihre Erkundung\u00A0...",
                "Les sous-fifres du monstre d'élite ont commencé à vous espionner...",
            ];
            string[] returns = [
                "特殊なリスキーモブの配下が、偵察活動を終えて帰還したようだ……",
                "The minions of an extraordinarily powerful mark have withdrawn...",
                "Die Helfer eines besonderen Hochwilds haben ihre Erkundung beendet.",
                "Les sous-fifres du monstre d'élite ont arrêté leur mission d'espionnage et ont déserté les lieux...",
            ];
            foreach (var message in starts)
            {
                Check(messages.Match(57, message + " 　") == AsMobPlate.Overlay.SsSystemEvent.Start);
                Check(messages.Match(11, message) == AsMobPlate.Overlay.SsSystemEvent.None);
                Check(messages.Match(57, "Report: " + message) == AsMobPlate.Overlay.SsSystemEvent.None);
            }
            foreach (var message in returns)
                Check(messages.Match(57, message) == AsMobPlate.Overlay.SsSystemEvent.Returned);
            Check(messages.Match(57, "強大なリスキーモブの気配を感じる……！") == AsMobPlate.Overlay.SsSystemEvent.None);
        });
        run("SS trigger accepts one alert for duplicate messages", () =>
        {
            var p = new HuntProgress();
            Check(p.Start(10));
            Check(!p.Start(11));
            Check(p.Start(100));
        });
        run("SS alert plays five spaced sounds despite ordinary detections", () =>
        {
            var sounds = new AsMobPlate.Overlay.NotificationSoundSequence();
            sounds.Queue(4, 0);
            sounds.Queue(5, 0, true);
            for (var i = 0; i < 5; i++)
            {
                var now = i * 0.25;
                sounds.Queue(4, now);
                Check(sounds.Take(now, 0.25));
                Check(!sounds.Take(now + 0.1, 0.25));
            }
            sounds.Queue(4, 1.1);
            Check(!sounds.Take(1.25, 0.25));
            sounds.Queue(4, 2);
            Check(sounds.Take(2, 0.25));
            sounds.Clear();
            Check(!sounds.Take(3, 0.25));
        });
        run("Arrival warning survives missing and improved estimates and disappearance", () =>
        {
            var p = new HuntProgress();
            Observe(p, 1, HuntRank.SS, false, 10);
            Check(!p.LatchArrivalWarning(1, 0x2977, null, null));
            Check(!p.LatchArrivalWarning(1, 0x2977, double.NaN, TimeSpan.FromSeconds(35)));
            Check(p.LatchArrivalWarning(1, 0x2977, 48, TimeSpan.FromSeconds(35)));
            Check(p.LatchArrivalWarning(1, 0x2977, null, null));
            Check(p.LatchArrivalWarning(1, 0x2977, 5, TimeSpan.FromSeconds(100)));
            p.Prune(2000, 30);
            Check(p.LatchArrivalWarning(1, 0x2977, null, null));
            Check(!p.LatchArrivalWarning(2, 0x2977, null, null));
            Check(!p.LatchArrivalWarning(1, 0x345E, null, null));
            Observe(p, 1, HuntRank.SS, true, 2001);
            Check(!p.LatchArrivalWarning(1, 0x2977, 48, TimeSpan.FromSeconds(35)));
            p.Clear();
            Observe(p, 1, HuntRank.SS, false, 2002);
            Check(!p.LatchArrivalWarning(1, 0x2977, null, null));
        });
        run("Verified hunt IDs exclude regular B ranks", () =>
        {
            var registry = new HuntMarkRegistry();
            foreach (var id in new uint[] { 0x22D4, 0x2978, 0x345F }) Check(registry.GetRank(id) == HuntRank.Minion);
            foreach (var id in new uint[] { 0x22D3, 0x2977, 0x345E }) Check(registry.GetRank(id) == HuntRank.SS);
            Check(registry.GetRank(1) == HuntRank.None);
            Check(registry.GetRank(10635) == HuntRank.None);
            Check(!HuntMarkRegistry.IsSsTerritory(819) && !HuntMarkRegistry.IsSsTerritory(962)
                && !HuntMarkRegistry.IsSsTerritory(1193));
        });
        run("Disappearance and five minutes do not mean defeat or failure", () =>
        {
            var p = new HuntProgress();
            p.Start(10);
            Observe(p, 1, HuntRank.Minion, false, 11);
            p.Prune(311, 30);
            Check(p.Stage == HuntStage.Searching && p.ObservedKills == 0 && p.Find(1) == null);
        });
        run("Observed kills are deduplicated and do not imply boss detection", () =>
        {
            var p = new HuntProgress();
            p.Start(10);
            for (ulong id = 1; id <= 4; id++)
            {
                Observe(p, id, HuntRank.Minion, true, 11);
                Observe(p, id, HuntRank.Minion, true, 12);
            }
            Check(p.ObservedKills == 4 && p.Stage == HuntStage.Searching);
            p.Start(12);
            Check(p.ObservedKills == 4);
            p.Start(600);
            Check(p.ObservedKills == 0);
        });
        run("Boss detection supersedes minions and death timestamps remain stable", () =>
        {
            var p = new HuntProgress();
            Observe(p, 1, HuntRank.SS, false, 10);
            Observe(p, 2, HuntRank.Minion, false, 11);
            Check(p.Stage == HuntStage.Boss);
            Observe(p, 1, HuntRank.SS, true, 20);
            Observe(p, 1, HuntRank.SS, true, 40);
            p.Return(41);
            Check(p.Stage == HuntStage.Defeated && p.ChangedAt == 20 && p.Find(1)!.DiedAt == 20);
        });
        run("Return and context reset cannot invent dead observations", () =>
        {
            var p = new HuntProgress();
            p.Start(10);
            Observe(p, 1, HuntRank.Minion, false, 11);
            p.Return(20);
            Check(p.Stage == HuntStage.Returned && p.Find(1)!.DiedAt == null);
            p.Clear();
            Check(p.Stage == HuntStage.None && p.StartedAt == null && p.Find(1) == null);
        });
        run("SS trigger highlights only the latest defeated S and never extends its lifetime", () =>
        {
            var p = new HuntProgress();
            Observe(p, 1, HuntRank.S, true, 10);
            Observe(p, 2, HuntRank.S, true, 15);
            Observe(p, 3, HuntRank.A, true, 16);
            Observe(p, 4, HuntRank.SS, true, 16);
            Check(p.Start(17));
            Check(!p.Find(1)!.SsTriggered && p.Find(2)!.SsTriggered && !p.Find(3)!.SsTriggered && !p.Find(4)!.SsTriggered);
            Check(!p.Start(18));
            p.Return(19);
            Observe(p, 5, HuntRank.SS, false, 20);
            Observe(p, 5, HuntRank.SS, true, 21);
            Check(p.Find(2)!.SsTriggered && p.Find(2)!.DiedAt == 15);
            p.Prune(46, 30);
            Check(p.Find(2) == null);
        });
        run("SS trigger waits three seconds for a death sample and clears with context", () =>
        {
            var p = new HuntProgress();
            p.Start(10);
            Observe(p, 1, HuntRank.S, true, 13);
            p.ResolveSsTrigger(13, 30);
            Check(p.Find(1)!.SsTriggered);
            p.Clear();
            Observe(p, 2, HuntRank.S, true, 14);
            p.ResolveSsTrigger(14, 30);
            Check(!p.Find(2)!.SsTriggered);
            p.Clear();
            p.Start(20);
            Observe(p, 3, HuntRank.S, true, 23.01);
            p.ResolveSsTrigger(23.01, 30);
            Check(!p.Find(3)!.SsTriggered);
        });
        run("Expired deaths and minion sightings cannot produce an S highlight", () =>
        {
            var p = new HuntProgress();
            Observe(p, 1, HuntRank.S, true, 10);
            Observe(p, 2, HuntRank.Minion, false, 11);
            p.ResolveSsTrigger(11, 30);
            Check(!p.Find(1)!.SsTriggered);
            p.Start(40);
            Check(!p.Find(1)!.SsTriggered);
            p.Clear();
            Observe(p, 3, HuntRank.S, true, 50);
            p.Start(52, 1);
            Check(!p.Find(3)!.SsTriggered);
        });
        run("Ineligible territory cannot start an SS event", () =>
        {
            var p = new HuntProgress();
            p.Observe(1, 0x2977, 1, "test", HuntRank.SS, Vector3.Zero, 2, false, true, false, 1);
            Check(p.Stage == HuntStage.None);
        });
    }

    private static void Check(bool value) { if (!value) throw new Exception("Hunt progress assertion failed."); }
    private static void Observe(HuntProgress p, ulong id, HuntRank rank, bool dead, double now)
        => p.Observe(id, rank == HuntRank.SS ? 0x2977u : 0x2978u, 1, "test", rank,
            Vector3.Zero, 2, dead, false, true, now);
}
