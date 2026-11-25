using System;
using System.IO;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace NaviFantasy;

public sealed class Plugin : IDalamudPlugin
{
    private const string CommandName = "/navi";
    private const float HeyListenIntervalSeconds = 30f;

    private readonly IDalamudPluginInterface pluginInterface;
    private readonly ICommandManager commandManager;
    private readonly IObjectTable objectTable;
    private readonly Configuration configuration;
    private readonly WindowSystem windowSystem = new("NaviFantasy");
    private readonly ConfigWindow configWindow;
    private readonly FairyRenderer fairy;
    private readonly AudioPlayer audio;

    private float secondsSinceLastHey;

    public Plugin(
        IDalamudPluginInterface pluginInterface,
        ICommandManager commandManager,
        ITextureProvider textureProvider,
        IObjectTable objectTable,
        IGameGui gameGui,
        ICondition condition,
        IPluginLog log)
    {
        this.pluginInterface = pluginInterface;
        this.commandManager = commandManager;
        this.objectTable = objectTable;

        configuration = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        var assets = Path.Combine(pluginInterface.AssemblyLocation.DirectoryName ?? string.Empty, "Assets");
        fairy = new FairyRenderer(configuration, Path.Combine(assets, "navi_wings_sheet.png"), Path.Combine(assets, "speech_bubble.png"), textureProvider, gameGui, objectTable, condition);
        audio = new AudioPlayer(Path.Combine(assets, "hey_listen.mp3"), log);

        configWindow = new ConfigWindow(configuration, () => pluginInterface.SavePluginConfig(configuration));
        windowSystem.AddWindow(configWindow);

        commandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Open the Navi Fantasy settings.",
        });

        pluginInterface.UiBuilder.Draw += Draw;
        pluginInterface.UiBuilder.OpenConfigUi += ToggleConfig;
        pluginInterface.UiBuilder.OpenMainUi += ToggleConfig;
    }

    private void OnCommand(string command, string args) => ToggleConfig();

    private void ToggleConfig() => configWindow.Toggle();

    private void Draw()
    {
        var dt = Math.Min(ImGui.GetIO().DeltaTime, 0.05f);

        windowSystem.Draw();
        fairy.Draw(dt);
        TickHeyListen(dt);
    }

    private void TickHeyListen(float dt)
    {
        // Only count time while a character is actually in the world.
        if (objectTable.LocalPlayer == null)
        {
            secondsSinceLastHey = 0f;
            return;
        }

        secondsSinceLastHey += dt;
        if (secondsSinceLastHey >= HeyListenIntervalSeconds)
        {
            secondsSinceLastHey = 0f;
            audio.Play();
            fairy.Say("Hey!"); // placeholder: later this will be a real tip
        }
    }

    public void Dispose()
    {
        pluginInterface.UiBuilder.Draw -= Draw;
        pluginInterface.UiBuilder.OpenConfigUi -= ToggleConfig;
        pluginInterface.UiBuilder.OpenMainUi -= ToggleConfig;
        commandManager.RemoveHandler(CommandName);
        windowSystem.RemoveAllWindows();
        audio.Dispose();
    }
}
