using System;
using System.Numerics;
using System.Text;
using AsMobPlate.Hunts;
using AsMobPlate.Localization;
using Dalamud.Game.Gui.PartyFinder.Types;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace AsMobPlate.Overlay;

public sealed class PartyFinderRecruitment : IDisposable
{
    internal const ulong TankAndHealerJobFlags = (ulong)(JobFlags.Paladin | JobFlags.Warrior
        | JobFlags.DarkKnight | JobFlags.Gunbreaker | JobFlags.WhiteMage | JobFlags.Scholar
        | JobFlags.Astrologian | JobFlags.Sage);

    private readonly Configuration configuration;
    private readonly IClientState clientState;
    private readonly IChatGui chatGui;
    private readonly IPluginLog pluginLog;
    private readonly IGameGui gameGui;
    private readonly IFramework framework;
    private readonly IObjectTable objectTable;
    private readonly DebugLog debugLog;
    private string? pendingComment;
    private uint territory;
    private long deadline;
    private long nextStep;
    private Stage stage;

    public PartyFinderRecruitment(Configuration configuration, IClientState clientState, IChatGui chatGui,
        IPluginLog pluginLog, IGameGui gameGui, IFramework framework, IObjectTable objectTable, DebugLog debugLog)
    {
        this.configuration = configuration;
        this.clientState = clientState;
        this.chatGui = chatGui;
        this.pluginLog = pluginLog;
        this.gameGui = gameGui;
        this.framework = framework;
        this.objectTable = objectTable;
        this.debugLog = debugLog;
        this.framework.Update += this.Update;
    }

    public void Open(HuntRank rank, string name, Vector2 mapPosition, string area, string? startEt)
    {
        if (rank == HuntRank.Minion || this.pendingComment != null)
            return;
        this.pendingComment = RecruitmentComment.Build(this.configuration.Language, rank, name, area, mapPosition, startEt);
        this.territory = this.clientState.TerritoryType;
        this.deadline = Environment.TickCount64 + 10000;
        this.nextStep = 0;
        this.stage = Stage.OpenFinder;
        this.debugLog.Add($"PF requested: [{rank}] {name}; ET={startEt}");
    }

    public void Dispose()
    {
        this.pendingComment = null;
        this.framework.Update -= this.Update;
    }

    private unsafe void Update(IFramework _)
    {
        if (this.pendingComment == null)
            return;
        if (!this.configuration.EnablePartyFinderOnRightClick || !this.clientState.IsLoggedIn
            || this.clientState.TerritoryType != this.territory)
        {
            this.pendingComment = null;
            return;
        }
        if (Environment.TickCount64 >= this.deadline)
        {
            this.Fail("Recruitment failed");
            return;
        }
        if (Environment.TickCount64 < this.nextStep)
            return;
        try
        {
            var agent = AgentLookingForGroup.Instance();
            if (agent == null)
                return;
            var editor = (AddonLookingForGroupCondition*)this.gameGui.GetAddonByName("LookingForGroupCondition").Address;
            switch (this.stage)
            {
                case Stage.OpenFinder:
                    // An existing draft/listing belongs to the user; never reset it as a side effect of a plate click.
                    if (editor != null && editor->IsVisible)
                    {
                        editor->Focus();
                        this.Fail("Recruitment already open");
                        return;
                    }
                    if (this.objectTable.LocalPlayer?.OnlineStatus.RowId == 26)
                    {
                        this.Fail("Recruitment already active");
                        return;
                    }
                    agent->SearchAreaTab = 0;
                    agent->Show();
                    this.Advance(Stage.WaitFinder);
                    break;
                case Stage.WaitFinder:
                    var finder = (AddonLookingForGroup*)this.gameGui.GetAddonByName("LookingForGroup").Address;
                    if (finder == null || !finder->IsReady || !finder->IsVisible)
                        return;
                    agent->SearchAreaTab = 0;
                    // Only the list's edit/open button is dispatched, never the editor's registration button.
                    if (!ClickButton(finder->RecruitMembersButton))
                        return;
                    this.Advance(Stage.WaitEditor);
                    break;
                case Stage.WaitEditor:
                    if (editor == null || !editor->IsReady || !editor->IsVisible)
                        return;
                    ApplySettings(agent, this.pendingComment);
                    agent->PopulateRecruitmentCriteriaPopup(false, false);
                    this.Advance(Stage.FillControls);
                    break;
                case Stage.FillControls:
                    if (editor == null || !editor->IsVisible)
                    {
                        this.pendingComment = null;
                        return;
                    }
                    if (!editor->IsReady || editor->CommentTextInput == null)
                        return;
                    // Initialization can replace slot flags. Apply once after it completes, then stop writing.
                    ApplySettings(agent, this.pendingComment);
                    editor->CommentTextInput->SetText(this.pendingComment);
                    SetChecked(editor->FormPrivatePartyCheckbox, false);
                    SetChecked(editor->LimitToWorldServerCheckbox, true);
                    SetChecked(editor->OnePlayerPerJobCheckbox, false);
                    SetChecked(editor->RemoveRoleRestrictionsCheckBox, false);
                    SetChecked(editor->UnselectClassesCheckbox, true);
                    SetChecked(editor->AvgItemLevelCheckbox, false);
                    SetChecked(editor->CompletionStatusCheckBox, false);
                    SetChecked(editor->BeginnersWelcomeCheckBox, false);
                    SetChecked(editor->UnrestrictedPartyCheckBox, false);
                    SetChecked(editor->MinimumItemLevelCheckBox, false);
                    SetChecked(editor->SilenceEchoCheckbox, false);
                    for (var i = 0; i < 4; i++)
                        SetChecked(editor->Languages[i].Value, true);
                    editor->Focus();
                    this.debugLog.Add($"PF editor filled: category=Hunt; empty-slot mask=0x{TankAndHealerJobFlags:X}; comment={agent->StoredRecruitmentInfo.CommentString}");
                    this.pendingComment = null;
                    break;
            }
        }
        catch (Exception ex)
        {
            this.pluginLog.Warning(ex, "Failed to prepare hunt recruitment.");
            this.Fail("Recruitment failed");
        }
    }

    internal static unsafe void ApplySettings(AgentLookingForGroup* agent, string comment)
    {
        var info = &agent->StoredRecruitmentInfo;
        // Preserve the game's member identities and occupied slots.
        info->SelectedCategory = AgentLookingForGroup.DutyCategory.TheHunt;
        info->SelectedDutyId = 0;
        *((byte*)info + 0x12) = 0; // Category-wide content, not a ContentFinderCondition row.
        info->Objective = AgentLookingForGroup.Objective.None;
        info->BeginnerFriendly = 0;
        info->CompletionStatus = AgentLookingForGroup.CompletionStatus.None;
        info->DutyFinderSettingFlags = AgentLookingForGroup.DutyFinderSetting.None;
        info->LootRule = AgentLookingForGroup.LootRule.Normal;
        info->Password = 10000; // Native sentinel for a public listing.
        info->LanguageFlags = AgentLookingForGroup.Language.Japanese | AgentLookingForGroup.Language.English
            | AgentLookingForGroup.Language.German | AgentLookingForGroup.Language.French;
        info->NumberOfSlotsInMainParty = 8;
        info->LimitRecruitingToWorld = 0; // Zero enables world-only recruitment.
        info->OnePlayerPerJob = 0;
        info->NumberOfGroups = 1;
        agent->AvgItemLvEnabled = 0;
        for (var i = 1; i < 8; i++)
        {
            if (info->MemberContentIds[i] == 0)
                info->SlotFlags[i] = TankAndHealerJobFlags;
        }
        // Clear the whole buffer: the generated string setter can leave bytes from a longer previous comment.
        info->Comment.Clear();
        Encoding.UTF8.GetBytes(comment, info->Comment[..190]);
    }

    private static unsafe bool ClickButton(AtkComponentButton* button)
    {
        if (button == null || button->OwnerNode == null || !button->IsEnabled)
            return false;
        for (var ev = button->OwnerNode->AtkEventManager.Event; ev != null; ev = ev->NextEvent)
        {
            if (ev->State.EventType != AtkEventType.ButtonClick || ev->Listener == null)
                continue;
            var click = *ev;
            AtkEventData data = default;
            ev->Listener->ReceiveEvent(AtkEventType.ButtonClick, (int)ev->Param, &click, &data);
            return true;
        }
        return false;
    }

    private static unsafe void SetChecked(AtkComponentCheckBox* checkbox, bool value)
    {
        if (checkbox != null)
            checkbox->SetChecked(value);
    }

    private void Advance(Stage next)
    {
        this.stage = next;
        this.nextStep = Environment.TickCount64 + 150;
        this.debugLog.Add($"PF stage: {next}");
    }

    private void Fail(string key)
    {
        this.debugLog.Add($"PF stopped at {this.stage}: {key}");
        this.pendingComment = null;
        this.chatGui.PrintError($"[AS Mob Plate] {UiText.Get(key, this.configuration.Language)}");
    }

    private enum Stage { OpenFinder, WaitFinder, WaitEditor, FillControls }
}
