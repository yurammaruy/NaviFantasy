using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace NaviFantasy;

public sealed class Plugin : IDalamudPlugin
{
    private const string CommandName = "/navi";
    private const string FactCommandName = "/ykff14"; // "You Know FF14"

    private readonly IDalamudPluginInterface pluginInterface;
    private readonly ICommandManager commandManager;
    private readonly IObjectTable objectTable;
    private readonly IClientState clientState;
    private readonly IPartyList partyList;
    private readonly IChatGui chatGui;
    private readonly IGameGui gameGui;
    private readonly BossRadar radar;
    private readonly BossMemory memory;
    private readonly RadarWindow radarWindow;
    private readonly ICondition condition;
    private readonly Configuration configuration;
    private readonly WindowSystem windowSystem = new("NaviFantasy");
    private readonly ConfigWindow configWindow;
    private readonly FairyRenderer fairy;
    private readonly AudioPlayer audio;
    private readonly AlertEngine engine;
    private readonly HudLocator hud;
    private readonly DailyTracker dailies;
    private readonly MailWatcher mailWatcher;

    private HudPerch perch = HudPerch.None;
    private string? perchName;
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
        IAddonLifecycle addonLifecycle,
        IPluginLog log)
    {
        this.pluginInterface = pluginInterface;
        this.commandManager = commandManager;
        this.objectTable = objectTable;
        this.clientState = clientState;
        this.partyList = partyList;
        this.chatGui = chatGui;
        this.condition = condition;
        this.gameGui = gameGui;

        configuration = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        Loc.Current = configuration.Language;

        var assets = Path.Combine(pluginInterface.AssemblyLocation.DirectoryName ?? string.Empty, "Assets");
        fairy = new FairyRenderer(configuration, assets, textureProvider, gameGui, objectTable, condition);
        audio = new AudioPlayer(Path.Combine(assets, "hey_listen.mp3"), log);
        dailies = new DailyTracker(dataManager, log);
        mailWatcher = new MailWatcher(addonLifecycle, gameGui);
        var facts = new FunFacts(assets, log);
        var logPath = Path.Combine(pluginInterface.ConfigDirectory.FullName, "bosslog.csv");
        radar = new BossRadar(configuration, objectTable, dataManager, clientState, log, logPath);
        var tips = new MechanicTips(Path.Combine(assets, "mechanics.json"), log);
        memory = new BossMemory(configuration, radar, objectTable, condition, clientState, tips, Path.Combine(pluginInterface.ConfigDirectory.FullName, "learned"), log);
        engine = new AlertEngine(configuration, objectTable, partyList, dataManager, condition, facts, clientState, dailies, mailWatcher, radar, tips, memory);
        hud = new HudLocator(gameGui);

        configWindow = new ConfigWindow(configuration, () => pluginInterface.SavePluginConfig(configuration), dailies, () =>
        {
            memory.ForgetAll();
            Say(Loc.Get("forget_done"));
        });
        windowSystem.AddWindow(configWindow);

        radarWindow = new RadarWindow(configuration, radar, memory);
        windowSystem.AddWindow(radarWindow);

        commandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = Loc.Get("cmd_help"),
        });

        commandManager.AddHandler(FactCommandName, new CommandInfo(OnFactCommand)
        {
            HelpMessage = Loc.Get("cmd_fact_help"),
        });

        pluginInterface.UiBuilder.Draw += Draw;
        pluginInterface.UiBuilder.OpenConfigUi += ToggleConfig;
        pluginInterface.UiBuilder.OpenMainUi += ToggleConfig;
    }

    private void ToggleConfig() => configWindow.Toggle();

    private void Say(string text) => chatGui.Print("[Navi] " + text);

    private void Draw()
    {
        var dt = Math.Min(ImGui.GetIO().DeltaTime, 0.05f);

        // The radar window is shown when the boss helper and the radar are on (and never in PvP).
        var radarWanted = configuration.BossHelperEnabled && configuration.RadarWindowVisible && !clientState.IsPvP;
        radarWindow.IsOpen = radarWanted;

        windowSystem.Draw();

        if (radarWanted && !radarWindow.IsOpen) // the player closed it with the X button
        {
            configuration.RadarWindowVisible = false;
            pluginInterface.SavePluginConfig(configuration);
        }

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

        if (configuration.BossHelperEnabled)
        {
            radar.Update(dt);
            if (configuration.RadarFloorOverlay)
                radar.DrawFloor(gameGui);
        }

        if (configuration.BossHelperEnabled && configuration.LearningEnabled)
            memory.Update(dt);
        else
            memory.Cancel();

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
        fairy.Announce(alert.Text, alert.Duration, alert.Perch != HudPerch.None, alert.Urgent);
        perch = alert.Perch;
        perchName = alert.PerchName;
        perchRefresh = 0f;
    }

    /// <summary>While an alert that points at something on the HUD is active, keep telling the fairy where it is.</summary>
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

        // If the target is not found this frame, she simply stays where she was.
        switch (perch)
        {
            case HudPerch.PartyMember:
                if (perchName != null && hud.TryLocatePartyMember(perchName, out var nameCenter, out var nameSize))
                    fairy.SetPerch(nameCenter, nameSize, beside: true);
                break;

            case HudPerch.Hp:
            case HudPerch.Mp:
            {
                var isHp = perch == HudPerch.Hp;
                var value = isHp ? player.CurrentHp : player.CurrentMp;
                if (hud.TryLocate(value, !isHp, out var center))
                    fairy.SetPerch(center);
                break;
            }
        }
    }

    private void OnFactCommand(string command, string args)
    {
        if (clientState.IsPvP)
        {
            Say(Loc.Get("pvp_off"));
            return;
        }

        if (objectTable.LocalPlayer == null || !engine.MakeFactAlert(out var alert))
        {
            Say(Loc.Get("no_facts"));
            return;
        }

        Raise(alert);
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
            case "roulettes":
                PrintRoulettes();
                break;
            case "mail":
                PrintMail();
                break;
            case "radar":
                ToggleRadar();
                break;
            case "record":
                ToggleRecording();
                break;
            default:
                Say(Loc.Get("cmd_usage"));
                break;
        }
    }

    private string RoleName(int role) => role switch
    {
        1 => Loc.Get("role_tank"),
        2 => Loc.Get("role_melee"),
        3 => Loc.Get("role_ranged"),
        4 => Loc.Get("role_healer"),
        _ => Loc.Get("role_none"),
    };

    private static string YesNo(bool value) => Loc.Get(value ? "yes" : "no");

    private void PrintStatus()
    {
        var player = objectTable.LocalPlayer;
        if (player == null || !engine.TryGetProfile(out var me))
        {
            Say(Loc.Get("no_char"));
            return;
        }

        Say(Loc.Get("status_hello", me.FirstName, me.JobName, me.JobAbbreviation, RoleName(me.Role)));

        Say(Loc.Get("status_learned", memory.KnownBossCount));
        Say($"Radar: cast position field = {BossRadar.CastLocationFieldName}");
        var pendingDailies = engine.PendingDailies();
        Say(Loc.Get("status_daily", pendingDailies.Count == 0 ? Loc.Get("none_word") : string.Join(", ", pendingDailies)));

        var use = engine.CurrentTerritoryUse();
        Say(Loc.Get("status_area", use == uint.MaxValue ? "?" : use.ToString(), YesNo(engine.IsInSafeArea()), (engine.SafeSeconds / 60f).ToString("0.0")));

        var pvp = clientState.IsPvP ? Loc.Get("pvp_on") : Loc.Get("no");
        Say(Loc.Get("status_vitals", player.CurrentHp, player.MaxHp, player.CurrentMp, player.MaxMp, partyList.Length, pvp, YesNo(condition[ConditionFlag.InCombat])));
    }

    private void PrintRoulettes()
    {
        var chosen = configuration.DailyRouletteIds != null ? new HashSet<uint>(configuration.DailyRouletteIds) : dailies.DefaultIds();
        if (dailies.Roulettes.Count == 0)
        {
            Say(Loc.Get("roulettes_fail"));
            return;
        }

        foreach (var r in dailies.Roulettes)
        {
            var state = Loc.Get(dailies.IsComplete(r.Id) ? "done_today" : "pending_word");
            var tracked = chosen.Contains(r.Id) ? Loc.Get("tracked_tag") : string.Empty;
            Say($"{r.Id}: {r.Name} - {state}{tracked}");
        }
    }

    private void ToggleRadar()
    {
        configuration.RadarWindowVisible = !configuration.RadarWindowVisible;
        if (configuration.RadarWindowVisible)
            configuration.BossHelperEnabled = true;
        pluginInterface.SavePluginConfig(configuration);
        Say(Loc.Get(configuration.RadarWindowVisible ? "radar_on" : "radar_off"));
    }

    private void ToggleRecording()
    {
        radar.Recording = !radar.Recording;
        Say(radar.Recording ? Loc.Get("record_on", radar.LogPath) : Loc.Get("record_off"));
    }

    private void PrintMail()
    {
        var seen = mailWatcher.Describe();
        if (seen.Count == 0)
        {
            Say(Loc.Get("mail_none_seen"));
        }
        else
        {
            foreach (var (name, visible) in seen)
                Say($"HUD: {name} - {Loc.Get(visible ? "visible_word" : "hidden_word")}");
        }

        Say(Loc.Get("mail_guess", YesNo(mailWatcher.HasUnreadMail())));
    }

    private void RunTest(string what)
    {
        if (clientState.IsPvP)
        {
            Say(Loc.Get("pvp_off"));
            return;
        }

        if (!engine.TryGetProfile(out var me))
        {
            Say(Loc.Get("no_char"));
            return;
        }

        // For the healer test, point at a real party member (so you can see her fly to the party list).
        string? ally = null;
        foreach (var member in partyList)
        {
            var name = member.Name.TextValue;
            if (name != me.FullName)
            {
                ally = name;
                break;
            }
        }

        var samplePending = engine.PendingDailies();
        if (samplePending.Count == 0)
            samplePending = new List<string> { "Leveling", "Trials", "Main Scenario" }; // nothing pending: show a sample

        Alert? alert = what switch
        {
            "hp" => new Alert(AlertKind.Test, Loc.Get("hp_low", me.FirstName), 70, HudPerch.Hp, 4.5f),
            "mp" => new Alert(AlertKind.Test, Loc.Get("mp_low", me.FirstName), 40, HudPerch.Mp, 4.5f),
            "heal" => new Alert(
                AlertKind.Test,
                Loc.Get("ally_heal", ally != null ? AlertEngine.FirstName(ally) : "Fulano"),
                60,
                ally != null ? HudPerch.PartyMember : HudPerch.None,
                3.5f,
                ally),
            "tank" => new Alert(AlertKind.Test, Loc.Get("allies_hurt", me.FirstName), 50, HudPerch.None, 4f),
            "boss" => new Alert(AlertKind.Test, Loc.Get("boss_cast_tip", "Boss", "Devastating Blow", Loc.Get("tip_line")), 80, HudPerch.None, 5f),
            "danger" => new Alert(AlertKind.Test, Loc.Get("danger_now", me.FirstName, Loc.Get("adv_line")), 100, HudPerch.None, 4f, null, true),
            "fact" => engine.MakeFactAlert(out var fact) ? fact : null,
            "daily" => engine.MakeDailyAlert(me.FirstName, samplePending),
            "mail" => engine.MakeMailAlert(me.FirstName),
            "water" => AlertEngine.MakeHydrationAlert(),
            _ => null,
        };

        if (alert == null)
        {
            Say(Loc.Get("test_usage"));
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
        commandManager.RemoveHandler(FactCommandName);
        windowSystem.RemoveAllWindows();
        audio.Dispose();
        mailWatcher.Dispose();
    }
}
