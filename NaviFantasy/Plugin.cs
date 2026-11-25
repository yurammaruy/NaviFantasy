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

    private readonly IDalamudPluginInterface pluginInterface;
    private readonly ICommandManager commandManager;
    private readonly IObjectTable objectTable;
    private readonly IClientState clientState;
    private readonly IPartyList partyList;
    private readonly IChatGui chatGui;
    private readonly ICondition condition;
    private readonly Configuration configuration;
    private readonly WindowSystem windowSystem = new("NaviFantasy");
    private readonly ConfigWindow configWindow;
    private readonly FairyRenderer fairy;
    private readonly AudioPlayer audio;
    private readonly AlertEngine engine;
    private readonly HudLocator hud;

    private HudPerch perch = HudPerch.None;
    private float perchRefresh;
    private bool wasPvP;

    public Plugin(
        IDalamudPluginInterface pluginInterface,
        ICommandManager commandManager,
        ITextureProvider textureProvider,
        IObjectTable objectTable,
        IGameGui gameGui,
        ICondition condition,
        IClientState clientState,
        IPartyList partyList,
        IDataManager dataManager,
        IChatGui chatGui,
        IPluginLog log)
    {
        this.pluginInterface = pluginInterface;
        this.commandManager = commandManager;
        this.objectTable = objectTable;
        this.clientState = clientState;
        this.partyList = partyList;
        this.chatGui = chatGui;
        this.condition = condition;

        configuration = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        var assets = Path.Combine(pluginInterface.AssemblyLocation.DirectoryName ?? string.Empty, "Assets");
        fairy = new FairyRenderer(configuration, assets, textureProvider, gameGui, objectTable, condition);
        audio = new AudioPlayer(Path.Combine(assets, "hey_listen.mp3"), log);
        engine = new AlertEngine(configuration, objectTable, partyList, dataManager, condition);
        hud = new HudLocator(gameGui);

        configWindow = new ConfigWindow(configuration, () => pluginInterface.SavePluginConfig(configuration));
        windowSystem.AddWindow(configWindow);

        commandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Navi Fantasy: /navi (settings), /navi status, /navi test <hp|mp|heal|tank|boss|danger>",
        });

        pluginInterface.UiBuilder.Draw += Draw;
        pluginInterface.UiBuilder.OpenConfigUi += ToggleConfig;
        pluginInterface.UiBuilder.OpenMainUi += ToggleConfig;
    }

    private void ToggleConfig() => configWindow.Toggle();

    private void Draw()
    {
        var dt = Math.Min(ImGui.GetIO().DeltaTime, 0.05f);

        windowSystem.Draw();

        // PvP: the fairy is switched off (PvP features will come later).
        if (clientState.IsPvP)
        {
            if (!wasPvP)
            {
                fairy.Suspend();
                engine.Reset();
                perch = HudPerch.None;
                wasPvP = true;
            }

            return;
        }

        wasPvP = false;

        var alert = engine.Update(dt);
        if (alert != null)
            Raise(alert);

        UpdatePerch(dt);
        fairy.Draw(dt);
    }

    /// <summary>"Hey!" sound + bubble, then the actual message.</summary>
    private void Raise(Alert alert)
    {
        audio.Play();
        fairy.Announce(alert.Text, alert.Duration, alert.Perch != HudPerch.None);
        perch = alert.Perch;
        perchRefresh = 0f;
    }

    /// <summary>While an HP/MP alert is active, keep telling the fairy where that number is on the HUD.</summary>
    private void UpdatePerch(float dt)
    {
        if (perch == HudPerch.None)
            return;

        if (!fairy.IsPerchActive)
        {
            perch = HudPerch.None;
            fairy.SetPerch(null);
            return;
        }

        perchRefresh -= dt;
        if (perchRefresh > 0f)
            return;
        perchRefresh = 0.25f;

        var player = objectTable.LocalPlayer;
        if (player == null)
            return;

        var isHp = perch == HudPerch.Hp;
        var value = isHp ? player.CurrentHp : player.CurrentMp;
        if (hud.TryLocate(value, !isHp, out var center))
            fairy.SetPerch(center); // if it is not found this frame, she simply stays where she was
    }

    private void OnCommand(string command, string args)
    {
        var parts = args.Trim().ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            ToggleConfig();
            return;
        }

        switch (parts[0])
        {
            case "status":
                PrintStatus();
                break;
            case "test":
                RunTest(parts.Length > 1 ? parts[1] : string.Empty);
                break;
            default:
                chatGui.Print("[Navi] Comandos: /navi (configurações), /navi status, /navi test <hp|mp|heal|tank|boss|danger>");
                break;
        }
    }

    private void PrintStatus()
    {
        var player = objectTable.LocalPlayer;
        if (player == null || !engine.TryGetProfile(out var me))
        {
            chatGui.Print("[Navi] Nenhum personagem encontrado.");
            return;
        }

        var role = me.Role switch
        {
            1 => "Tank",
            2 => "DPS corpo a corpo",
            3 => "DPS à distância",
            4 => "Healer",
            _ => "sem função de combate",
        };

        chatGui.Print($"[Navi] Olá, {me.FirstName}! Você é {me.JobName} ({me.JobAbbreviation}), função: {role}.");
        chatGui.Print($"[Navi] HP {player.CurrentHp}/{player.MaxHp} | MP {player.CurrentMp}/{player.MaxMp} | grupo: {partyList.Length} | PvP: {(clientState.IsPvP ? "sim (fada desativada)" : "não")} | em combate: {(condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.InCombat] ? "sim" : "não")}");
    }

    private void RunTest(string what)
    {
        if (clientState.IsPvP)
        {
            chatGui.Print("[Navi] A fada está desativada no PvP.");
            return;
        }

        if (!engine.TryGetProfile(out var me))
        {
            chatGui.Print("[Navi] Nenhum personagem encontrado.");
            return;
        }

        Alert? alert = what switch
        {
            "hp" => new Alert(AlertKind.Test, $"{me.FirstName}, seu HP está baixo (teste)!", 70, HudPerch.Hp, 4.5f),
            "mp" => new Alert(AlertKind.Test, $"{me.FirstName}, sua mana está baixa (teste)!", 40, HudPerch.Mp, 4.5f),
            "heal" => new Alert(AlertKind.Test, "Fulano precisa de cura! (teste)", 60, HudPerch.None, 3.5f),
            "tank" => new Alert(AlertKind.Test, $"{me.FirstName}, seus aliados estão sofrendo dano! (teste)", 50, HudPerch.None, 4f),
            "boss" => new Alert(AlertKind.Test, "Chefe de Teste está preparando Golpe Devastador!", 80, HudPerch.None, 4f),
            "danger" => new Alert(AlertKind.Test, $"Sai daí, {me.FirstName}! Golpe Devastador vai te acertar! (teste)", 100, HudPerch.None, 4f),
            _ => null,
        };

        if (alert == null)
        {
            chatGui.Print("[Navi] Use: /navi test <hp|mp|heal|tank|boss|danger>");
            return;
        }

        Raise(alert);
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
