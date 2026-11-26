using System;
using System.Collections.Generic;
using Dalamud.Game;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using LuminaRoulette = Lumina.Excel.Sheets.ContentRoulette;

namespace NaviFantasy;

/// <summary>
/// Knows which Duty Roulettes exist and whether today's daily bonus of each one is still pending
/// (the same data the Duty Finder shows). Completion comes from the game's own roulette state.
/// </summary>
public sealed unsafe class DailyTracker
{
    public readonly record struct Roulette(uint Id, string Name, bool DefaultSelected);

    // Roulettes monitored out of the box: the ones a typical max-level character sees in the Duty Finder.
    private static readonly string[] DefaultKeywords = { "Leveling", "Dungeons", "Trials", "Main Scenario", "Guildhests", "Alliance Raids" };

    private readonly IDataManager dataManager;
    private readonly IPluginLog log;
    private List<Roulette>? cache;

    public DailyTracker(IDataManager dataManager, IPluginLog log)
    {
        this.dataManager = dataManager;
        this.log = log;
    }

    public IReadOnlyList<Roulette> Roulettes => cache ??= Load();

    public HashSet<uint> DefaultIds()
    {
        var ids = new HashSet<uint>();
        foreach (var r in Roulettes)
            if (r.DefaultSelected)
                ids.Add(r.Id);
        return ids;
    }

    public bool IsComplete(uint rouletteId)
    {
        var content = InstanceContent.Instance();
        return content != null && content->IsRouletteComplete((byte)rouletteId);
    }

    /// <summary>Names of the selected roulettes whose daily bonus has not been collected yet. Null selection = the default set.</summary>
    public List<string> GetPending(IReadOnlyCollection<uint>? selected)
    {
        var chosen = selected != null ? new HashSet<uint>(selected) : DefaultIds();
        var pending = new List<string>();
        foreach (var r in Roulettes)
            if (chosen.Contains(r.Id) && !IsComplete(r.Id))
                pending.Add(r.Name);
        return pending;
    }

    private List<Roulette> Load()
    {
        var list = new List<Roulette>();
        try
        {
            // The English names are only used to recognise each roulette; what we show is in the client's own language.
            var english = dataManager.GetExcelSheet<LuminaRoulette>(ClientLanguage.English);
            var local = dataManager.GetExcelSheet<LuminaRoulette>();

            foreach (var row in english)
            {
                var en = row.Name.ExtractText();
                if (string.IsNullOrWhiteSpace(en) || !en.Contains("Roulette", StringComparison.OrdinalIgnoreCase))
                    continue;

                var shown = en;
                if (local.TryGetRow(row.RowId, out var localRow))
                {
                    var t = localRow.Name.ExtractText();
                    if (!string.IsNullOrWhiteSpace(t))
                        shown = t;
                }

                var isDefault = false;
                foreach (var key in DefaultKeywords)
                    if (en.Contains(key, StringComparison.OrdinalIgnoreCase))
                        isDefault = true;

                list.Add(new Roulette(row.RowId, ShortName(shown), isDefault));
            }
        }
        catch (Exception ex)
        {
            log.Error(ex, "Could not load the duty roulette list.");
        }

        return list;
    }

    /// <summary>"Duty Roulette: Leveling" -> "Leveling".</summary>
    private static string ShortName(string name)
    {
        var cut = Math.Max(name.LastIndexOf(':'), name.LastIndexOf('：'));
        return (cut >= 0 ? name[(cut + 1)..] : name).Trim();
    }
}
