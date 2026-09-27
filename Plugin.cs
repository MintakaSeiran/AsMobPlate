using System;
using Dalamud.Game.Command;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using AsMobPlate.Hunts;
using AsMobPlate.Overlay;
using AsMobPlate.Windows;

namespace AsMobPlate;

// Feature behavior is documented in README.md. Keep runtime code focused on
// wiring Dalamud services to the renderer/settings objects.
public sealed class Plugin : IDalamudPlugin
{
    private const string CommandName = "/asmobplate";

    private readonly IDalamudPluginInterface pluginInterface;
    private readonly ICommandManager commandManager;
    private readonly Configuration configuration;
    private readonly ConfigWindow configWindow;
    private readonly HuntNameplateRenderer renderer;

    public Plugin(
        IDalamudPluginInterface pluginInterface,
        ICommandManager commandManager,
        IObjectTable objectTable,
        IGameGui gameGui,
        IClientState clientState,
        IChatGui chatGui,
        IFramework framework,
        IPluginLog pluginLog)
    {
        this.pluginInterface = pluginInterface;
        this.commandManager = commandManager;

        this.configuration = this.pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        this.configuration.Initialize(this.pluginInterface);

        var registry = new HuntMarkRegistry();
        this.renderer = new HuntNameplateRenderer(this.configuration, registry, objectTable, gameGui, clientState, chatGui, framework, pluginLog);
        this.configWindow = new ConfigWindow(this.configuration, this.renderer.DebugLog);

        this.commandManager.AddHandler(CommandName, new CommandInfo(this.OnCommand)
        {
            HelpMessage = "Open AS Mob Plate settings.",
        });

        this.pluginInterface.UiBuilder.Draw += this.Draw;
        this.pluginInterface.UiBuilder.OpenConfigUi += this.OpenConfigUi;
    }

    public void Dispose()
    {
        this.pluginInterface.UiBuilder.OpenConfigUi -= this.OpenConfigUi;
        this.pluginInterface.UiBuilder.Draw -= this.Draw;
        this.commandManager.RemoveHandler(CommandName);
        this.renderer.Dispose();
    }

    private void OnCommand(string command, string arguments)
    {
        this.configWindow.IsOpen = !this.configWindow.IsOpen;
    }

    private void OpenConfigUi()
    {
        this.configWindow.IsOpen = true;
    }

    private void Draw()
    {
        this.renderer.Draw();
        this.configWindow.Draw();
    }
}
