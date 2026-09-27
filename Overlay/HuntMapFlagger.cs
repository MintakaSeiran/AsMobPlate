using System;
using System.Collections.Generic;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;

namespace AsMobPlate.Overlay;

public sealed class HuntMapFlagger
{
    private readonly Configuration configuration;
    private readonly IGameGui gameGui;
    private readonly IClientState clientState;
    private readonly IPluginLog pluginLog;
    private readonly HashSet<ulong> flaggedNow = new();
    private readonly HashSet<ulong> seenThisFrame = new();

    public HuntMapFlagger(Configuration configuration, IGameGui gameGui, IClientState clientState, IPluginLog pluginLog)
    {
        this.configuration = configuration;
        this.gameGui = gameGui;
        this.clientState = clientState;
        this.pluginLog = pluginLog;
    }

    public void BeginFrame()
    {
        this.seenThisFrame.Clear();
    }

    public void Seen(IBattleNpc npc)
    {
        var key = GetObjectKey(npc);
        this.seenThisFrame.Add(key);

        if (!this.configuration.EnableMapFlagOnDetection || this.flaggedNow.Contains(key))
            return;

        this.flaggedNow.Add(key);

        try
        {
            if (this.clientState.TerritoryType == 0 || this.clientState.MapId == 0)
                return;

            this.gameGui.OpenMapWithMapLink(this.clientState.TerritoryType, this.clientState.MapId, npc.Position);
        }
        catch (Exception ex)
        {
            this.pluginLog.Warning(ex, "Failed to place AS hunt map flag.");
        }
    }

    public void EndFrame()
    {
        this.flaggedNow.RemoveWhere(key => !this.seenThisFrame.Contains(key));
    }

    private static ulong GetObjectKey(IBattleNpc npc)
    {
        return npc.GameObjectId != 0 ? npc.GameObjectId : ((ulong)npc.NameId << 32) | npc.ObjectIndex;
    }
}
