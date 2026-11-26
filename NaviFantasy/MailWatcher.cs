using System;
using System.Collections.Generic;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgs;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace NaviFantasy;

/// <summary>
/// Best-effort detector of unread Moogle letters. The game has no documented "unread letters" value, so this watches
/// the HUD: it remembers the HUD addons (names starting with "_") that have "Letter" or "Mail" in their name and
/// reports unread mail while one of them is visible. Use /navi mail to see what it is looking at.
/// </summary>
public sealed unsafe class MailWatcher : IDisposable
{
    private readonly IAddonLifecycle lifecycle;
    private readonly IGameGui gameGui;
    private readonly HashSet<string> names = new();
    private readonly object gate = new();

    public MailWatcher(IAddonLifecycle lifecycle, IGameGui gameGui)
    {
        this.lifecycle = lifecycle;
        this.gameGui = gameGui;
        lifecycle.RegisterListener(AddonEvent.PostSetup, OnSetup);
        lifecycle.RegisterListener(AddonEvent.PreFinalize, OnFinalize);
    }

    private static bool IsRelevant(string name) =>
        name.StartsWith('_') &&
        (name.Contains("Letter", StringComparison.OrdinalIgnoreCase) ||
         name.Contains("Mail", StringComparison.OrdinalIgnoreCase) ||
         name.Contains("Notification", StringComparison.OrdinalIgnoreCase));

    private static bool LooksLikeMail(string name) =>
        name.Contains("Letter", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("Mail", StringComparison.OrdinalIgnoreCase);

    private void OnSetup(AddonEvent type, AddonArgs args)
    {
        var name = args.AddonName;
        if (!string.IsNullOrEmpty(name) && IsRelevant(name))
            lock (gate) names.Add(name);
    }

    private void OnFinalize(AddonEvent type, AddonArgs args)
    {
        var name = args.AddonName;
        if (!string.IsNullOrEmpty(name))
            lock (gate) names.Remove(name);
    }

    private bool IsVisible(string name)
    {
        var addon = (AtkUnitBase*)gameGui.GetAddonByName(name).Address;
        return addon != null && addon->IsVisible;
    }

    public bool HasUnreadMail()
    {
        foreach (var (name, visible) in Describe())
            if (visible && LooksLikeMail(name))
                return true;
        return false;
    }

    /// <summary>Every HUD addon with "Letter", "Mail" or "Notification" in its name that the plugin has seen, and whether it is visible.</summary>
    public List<(string Name, bool Visible)> Describe()
    {
        string[] snapshot;
        lock (gate)
        {
            snapshot = new string[names.Count];
            names.CopyTo(snapshot);
        }

        var result = new List<(string, bool)>();
        foreach (var name in snapshot)
            result.Add((name, IsVisible(name)));
        result.Sort((a, b) => string.CompareOrdinal(a.Item1, b.Item1));
        return result;
    }

    public void Dispose()
    {
        lifecycle.UnregisterListener(AddonEvent.PostSetup, OnSetup);
        lifecycle.UnregisterListener(AddonEvent.PreFinalize, OnFinalize);
    }
}
