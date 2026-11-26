using System;
using System.Numerics;
using System.Text;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace NaviFantasy;

/// <summary>
/// Finds where the HP / MP numbers are drawn on the HUD, wherever the player put them.
/// It reads the "_ParameterWidget" addon and looks for the text node that currently shows the given value.
/// </summary>
public sealed unsafe class HudLocator
{
    private const string ParameterAddon = "_ParameterWidget";

    private readonly IGameGui gameGui;

    public HudLocator(IGameGui gameGui)
    {
        this.gameGui = gameGui;
    }

    /// <param name="value">The HP or MP value the HUD is currently showing.</param>
    /// <param name="preferLast">If several numbers match (e.g. HP == MP), take the last one found (use it for MP).</param>
    public bool TryLocate(uint value, bool preferLast, out Vector2 center)
    {
        center = default;

        var ptr = gameGui.GetAddonByName(ParameterAddon);
        var addon = (AtkUnitBase*)ptr.Address;
        if (addon == null || !addon->IsVisible)
            return false;

        var matches = 0;
        Vector2 first = default, last = default;
        Collect(&addon->UldManager, value.ToString(), addon->Scale, 0, ref matches, ref first, ref last);
        if (matches > 0)
        {
            center = preferLast ? last : first;
            return true;
        }

        // Numbers hidden or not matched: fall back to the middle of the whole HP/MP widget.
        if (addon->RootNode != null)
        {
            var w = addon->RootNode->Width * addon->Scale;
            var h = addon->RootNode->Height * addon->Scale;
            center = new Vector2(addon->X + w * 0.5f, addon->Y + h * 0.5f);
            return true;
        }

        return false;
    }

    /// <summary>Finds the name of a party member in the HUD party list (so the fairy can fly to it).</summary>
    public bool TryLocatePartyMember(string fullName, out Vector2 center, out Vector2 size)
    {
        center = default;
        size = default;

        var addon = (AtkUnitBase*)gameGui.GetAddonByName("_PartyList").Address;
        if (addon == null || !addon->IsVisible || string.IsNullOrWhiteSpace(fullName))
            return false;

        var found = false;
        CollectText(&addon->UldManager, fullName, addon->Scale, 0, ref found, ref center, ref size);
        if (!found)
            CollectText(&addon->UldManager, AlertEngine.FirstName(fullName), addon->Scale, 0, ref found, ref center, ref size);

        return found;
    }

    private static void CollectText(AtkUldManager* uld, string needle, float scale, int depth, ref bool found, ref Vector2 center, ref Vector2 size)
    {
        if (found || uld == null || uld->NodeList == null || depth > 6)
            return;

        for (var i = 0; i < uld->NodeListCount && !found; i++)
        {
            var node = uld->NodeList[i];
            if (node == null)
                continue;

            var type = (int)node->Type;
            if (type == (int)NodeType.Text)
            {
                if ((node->NodeFlags & NodeFlags.Visible) == 0)
                    continue;

                var text = ((AtkTextNode*)node)->NodeText.ToString();
                if (!string.IsNullOrEmpty(text) && text.Contains(needle, StringComparison.OrdinalIgnoreCase))
                {
                    size = new Vector2(node->Width * scale, node->Height * scale);
                    center = new Vector2(node->ScreenX + size.X * 0.5f, node->ScreenY + size.Y * 0.5f);
                    found = true;
                }
            }
            else if (type >= 1000)
            {
                var comp = ((AtkComponentNode*)node)->Component;
                if (comp != null)
                    CollectText(&comp->UldManager, needle, scale, depth + 1, ref found, ref center, ref size);
            }
        }
    }

    private static void Collect(AtkUldManager* uld, string target, float scale, int depth, ref int matches, ref Vector2 first, ref Vector2 last)
    {
        if (uld == null || uld->NodeList == null || depth > 4)
            return;

        for (var i = 0; i < uld->NodeListCount; i++)
        {
            var node = uld->NodeList[i];
            if (node == null)
                continue;

            var type = (int)node->Type;
            if (type == (int)NodeType.Text)
            {
                if ((node->NodeFlags & NodeFlags.Visible) == 0)
                    continue;

                var text = ((AtkTextNode*)node)->NodeText.ToString();
                if (DigitsOf(text) == target)
                {
                    var c = new Vector2(node->ScreenX + node->Width * scale * 0.5f, node->ScreenY + node->Height * scale * 0.5f);
                    if (matches == 0) first = c;
                    last = c;
                    matches++;
                }
            }
            else if (type >= 1000) // component node: look inside it too
            {
                var comp = ((AtkComponentNode*)node)->Component;
                if (comp != null)
                    Collect(&comp->UldManager, target, scale, depth + 1, ref matches, ref first, ref last);
            }
        }
    }

    private static string DigitsOf(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        var sb = new StringBuilder(text.Length);
        foreach (var ch in text)
            if (ch >= '0' && ch <= '9')
                sb.Append(ch);
        return sb.ToString();
    }
}
