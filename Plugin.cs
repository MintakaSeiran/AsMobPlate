using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using AsMobPlate.Localization;
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
    private readonly IChatGui chatGui;
    private readonly List<string> registeredCommands = new();
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
        IPluginLog pluginLog,
        ICondition condition,
        IDataManager dataManager)
    {
        this.pluginInterface = pluginInterface;
        this.commandManager = commandManager;
        this.chatGui = chatGui;

        this.configuration = this.pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        this.configuration.Initialize(this.pluginInterface);

        var registry = new HuntMarkRegistry();
        this.renderer = new HuntNameplateRenderer(this.configuration, registry, objectTable, gameGui, clientState, chatGui, framework, pluginLog, condition, dataManager);
        this.configWindow = new ConfigWindow(this.configuration, this.renderer.DebugLog);

        foreach (var name in new[] { CommandName, "/amp", "/asmob", "/asm" })
        {
            if (this.commandManager.AddHandler(name, new CommandInfo(this.OnCommand)
                { HelpMessage = "AS Mob Plate settings. Use /amp help for commands." }))
                this.registeredCommands.Add(name);
            else
                pluginLog.Warning("AS Mob Plate could not register command {Command}: already in use.", name);
        }

        this.pluginInterface.UiBuilder.Draw += this.Draw;
        this.pluginInterface.UiBuilder.OpenConfigUi += this.OpenConfigUi;
        this.pluginInterface.UiBuilder.OpenMainUi += this.OpenConfigUi;
    }

    public void Dispose()
    {
        this.pluginInterface.UiBuilder.OpenConfigUi -= this.OpenConfigUi;
        this.pluginInterface.UiBuilder.OpenMainUi -= this.OpenConfigUi;
        this.pluginInterface.UiBuilder.Draw -= this.Draw;
        foreach (var name in this.registeredCommands)
            this.commandManager.RemoveHandler(name);
        this.renderer.Dispose();
    }

    private void OnCommand(string command, string arguments)
    {
        var args = arguments.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (args.Length == 0)
        {
            this.configWindow.IsOpen = !this.configWindow.IsOpen;
            return;
        }

        var action = args[0].ToLowerInvariant();
        var value = args.Length == 2 ? args[1].ToLowerInvariant() : string.Empty;
        if (args.Length == 1)
        {
            switch (action)
            {
                case "config": case "open": this.configWindow.IsOpen = true; return;
                case "close": this.configWindow.IsOpen = false; return;
                case "preview": this.configWindow.OpenPreview(); return;
                case "help": this.PrintHelp(); return;
                case "status":
                    this.Reply($"A={this.configuration.ShowARank}, S={this.configuration.ShowSRank}, distance={this.configuration.MaxDistance}y, sound={this.configuration.EnableNotificationSound}, TTS={this.configuration.EnableTts}, language={this.configuration.Language}");
                    return;
            }
        }

        var changed = false;
        if (args.Length == 2)
        {
            switch (action)
            {
                case "a": changed = SetSwitch(ref this.configuration.ShowARank, value); break;
                case "s": changed = SetSwitch(ref this.configuration.ShowSRank, value); break;
                case "sound": changed = SetSwitch(ref this.configuration.EnableNotificationSound, value); break;
                case "tts": changed = SetSwitch(ref this.configuration.EnableTts, value); break;
                case "hp": changed = SetSwitch(ref this.configuration.ShowHpBar, value); break;
                case "percent": changed = SetSwitch(ref this.configuration.ShowHpPercent, value); break;
                case "flag": changed = SetSwitch(ref this.configuration.EnableMapFlagOnDetection, value); break;
                case "countdown": changed = SetSwitch(ref this.configuration.EnableCountdownOnAnnouncedStart, value); break;
                case "distance":
                    if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var distance)
                        && float.IsFinite(distance) && distance >= 1 && distance <= 2000)
                    {
                        this.configuration.MaxDistance = distance;
                        changed = true;
                    }
                    break;
                case "lang":
                    if (value is "en" or "jp" or "de" or "fr")
                    {
                        this.configuration.Language = Enum.Parse<UiLanguage>(value, true);
                        changed = true;
                    }
                    break;
                case "log":
                    if (value == "clear")
                    {
                        this.renderer.DebugLog.Clear();
                        this.Reply("Log cleared.");
                        return;
                    }
                    if (value == "dump")
                    {
                        try
                        {
                            var path = ConfigWindow.GetDebugDumpPath();
                            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                            File.WriteAllText(path, this.renderer.DebugLog.Dump());
                            this.Reply($"Log saved: {path}");
                        }
                        catch (Exception ex)
                        {
                            this.Reply($"Could not save log: {ex.Message}");
                        }
                        return;
                    }
                    break;
            }
        }
        if (changed)
        {
            this.configuration.Save();
            this.Reply($"{action}: {value}");
        }
        else
            this.PrintHelp();
    }

    private static bool SetSwitch(ref bool setting, string value)
    {
        switch (value)
        {
            case "on": setting = true; return true;
            case "off": setting = false; return true;
            case "toggle": setting = !setting; return true;
            default: return false;
        }
    }

    private void Reply(string message) => this.chatGui.Print($"[AS Mob Plate] {message}");

    private void PrintHelp()
    {
        this.Reply("/amp = /asmobplate = /asmob = /asm | config, open, close, preview, status, help");
        this.Reply("/amp <a|s|sound|tts|hp|percent|flag|countdown> <on|off|toggle>");
        this.Reply("/amp distance <1-2000> | lang <en|jp|de|fr> | log <dump|clear>");
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
