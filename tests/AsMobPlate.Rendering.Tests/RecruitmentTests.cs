using System.Globalization;
using System.Numerics;
using System.Text;
using AsMobPlate.Hunts;
using AsMobPlate.Localization;
using AsMobPlate.Overlay;
using Dalamud.Game.Gui.PartyFinder.Types;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;

internal static class RecruitmentTests
{
    internal static unsafe void Run()
    {
        AgentLookingForGroup agent = default;
        agent.StoredRecruitmentInfo.MemberContentIds[0] = 101;
        agent.StoredRecruitmentInfo.MemberContentIds[2] = 102;
        agent.StoredRecruitmentInfo.SlotFlags[0] = (ulong)JobFlags.Bard;
        agent.StoredRecruitmentInfo.SlotFlags[2] = (ulong)JobFlags.BlackMage;
        agent.StoredRecruitmentInfo.Password = 1234;
        agent.StoredRecruitmentInfo.LimitRecruitingToWorld = 1;
        agent.AvgItemLvEnabled = 1;
        PartyFinderRecruitment.ApplySettings(&agent, "[S] Hunt / Start ET 11:20");
        Check(agent.StoredRecruitmentInfo.MemberContentIds[2] == 102, "Existing member identity lost");
        Check(agent.StoredRecruitmentInfo.SlotFlags[0] == (ulong)JobFlags.Bard, "Leader slot changed");
        Check(agent.StoredRecruitmentInfo.SlotFlags[2] == (ulong)JobFlags.BlackMage, "Occupied slot changed");
        for (var i = 1; i < 8; i++)
        {
            if (i == 2) continue;
            var accepted = agent.StoredRecruitmentInfo.SlotFlags[i];
            foreach (var job in Enum.GetValues<JobFlags>())
            {
                var expected = job is JobFlags.Paladin or JobFlags.Warrior or JobFlags.DarkKnight or JobFlags.Gunbreaker
                    or JobFlags.WhiteMage or JobFlags.Scholar or JobFlags.Astrologian or JobFlags.Sage;
                Check(((accepted & (ulong)job) != 0) == expected, $"Unexpected acceptance for {job} in slot {i}");
            }
        }
        Check(agent.StoredRecruitmentInfo.Password == 10000, "Listing not public");
        Check(agent.StoredRecruitmentInfo.LimitRecruitingToWorld == 0, "World-only restriction inverted");
        Check(agent.AvgItemLvEnabled == 0, "Old item level restriction retained");
        Check(agent.StoredRecruitmentInfo.SelectedCategory == AgentLookingForGroup.DutyCategory.TheHunt, "Wrong category");
        Check(agent.StoredRecruitmentInfo.CommentString == "[S] Hunt / Start ET 11:20", "Native comment round trip failed");

        var previousCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            foreach (var language in Enum.GetValues<UiLanguage>())
            {
                var comment = RecruitmentComment.Build(language, HuntRank.S, "Hunt", "Area", new Vector2(12.3f, 4.5f), "11:20");
                Check(comment.StartsWith("[S] Hunt / Area (12.3, 4.5)"), "Culture-dependent coordinates");
                Check(comment.EndsWith($"{UiText.Get("Recruitment start", language)} ET 11:20"), "Localized ET missing");
                Check(!comment.Contains("Tank") && !comment.Contains("Heiler"), "Role wording leaked into comment");
                var withoutEt = RecruitmentComment.Build(language, HuntRank.A, "Hunt", "Area", Vector2.One, null);
                Check(!withoutEt.Contains("ET"), "Unannounced ET included");
                var longName = string.Concat(Enumerable.Repeat("\u72e9\u308a\U0001F600", 100));
                var bounded = RecruitmentComment.Build(language, HuntRank.S, longName, "Area", Vector2.One, "11:20");
                Check(Encoding.UTF8.GetByteCount(bounded) <= 190, "Native buffer exceeded");
                Check(bounded.EndsWith("ET 11:20"), "Long name removed ET");
                var strictUtf8 = new UTF8Encoding(false, true);
                strictUtf8.GetBytes(bounded);
                PartyFinderRecruitment.ApplySettings(&agent, bounded);
                Check(agent.StoredRecruitmentInfo.CommentString == bounded,
                    $"Unicode native comment corrupted: expected {Encoding.UTF8.GetByteCount(bounded)} bytes; got {Encoding.UTF8.GetByteCount(agent.StoredRecruitmentInfo.CommentString)} bytes: {agent.StoredRecruitmentInfo.CommentString}");
            }
        }
        finally { CultureInfo.CurrentCulture = previousCulture; }
        Console.WriteLine("Recruitment settings passed: occupied slots preserved, tank/healer-only vacancies, world/public flags, four-language UTF-8 comments.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
