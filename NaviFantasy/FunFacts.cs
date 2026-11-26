using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Dalamud.Plugin.Services;

namespace NaviFantasy;

/// <summary>
/// Fun facts in every language. Portuguese comes from Assets/curiosidades.txt (written as "12. Title. Text");
/// the other languages come from Assets/facts.xx.txt (one fact per line, text only).
/// Facts come out in random order and none repeats until all of them were told. If a language file is missing,
/// the Portuguese facts are used instead.
/// </summary>
public sealed class FunFacts
{
    private readonly string assetsDir;
    private readonly IPluginLog log;
    private readonly Dictionary<NaviLanguage, List<string>> facts = new();
    private readonly Dictionary<NaviLanguage, List<int>> bags = new();

    public FunFacts(string assetsDir, IPluginLog log)
    {
        this.assetsDir = assetsDir;
        this.log = log;
    }

    private List<string> Load(NaviLanguage language)
    {
        if (facts.TryGetValue(language, out var cached))
            return cached;

        var list = Read(language);
        if (list.Count == 0 && language != NaviLanguage.Portuguese)
            list = Load(NaviLanguage.Portuguese); // missing translation: fall back to Portuguese

        facts[language] = list;
        return list;
    }

    private List<string> Read(NaviLanguage language)
    {
        var list = new List<string>();
        var fileName = language == NaviLanguage.Portuguese ? "curiosidades.txt" : $"facts.{Loc.Code(language)}.txt";
        var path = Path.Combine(assetsDir, fileName);

        try
        {
            if (!File.Exists(path))
            {
                log.Warning("Fun facts file not found: {Path}", path);
                return list;
            }

            foreach (var raw in File.ReadAllLines(path, Encoding.UTF8))
            {
                var line = raw.Trim();
                if (line.Length == 0)
                    continue;

                // Numbered lines ("12. Title. Text"): drop the number and the short title. Plain lines are used as they are.
                var numbered = Regex.Match(line, @"^\d+\s*[\.\)\-]\s*(.+)$");
                if (numbered.Success)
                {
                    var rest = numbered.Groups[1].Value;
                    var cut = rest.IndexOf(". ", StringComparison.Ordinal);
                    line = cut > 0 && cut < 60 ? rest[(cut + 2)..] : rest;
                }

                list.Add(line.Trim());
            }

            log.Information("Loaded {Count} fun facts from {File}.", list.Count, fileName);
        }
        catch (Exception ex)
        {
            log.Error(ex, "Failed to load fun facts from {File}.", fileName);
        }

        return list;
    }

    public bool TryNext(out string text)
    {
        text = string.Empty;
        var language = Loc.Current;
        var list = Load(language);
        if (list.Count == 0)
            return false;

        if (!bags.TryGetValue(language, out var bag))
        {
            bag = new List<int>();
            bags[language] = bag;
        }

        if (bag.Count == 0)
        {
            for (var i = 0; i < list.Count; i++)
                bag.Add(i);

            for (var i = bag.Count - 1; i > 0; i--) // shuffle
            {
                var j = Random.Shared.Next(i + 1);
                (bag[i], bag[j]) = (bag[j], bag[i]);
            }
        }

        var index = bag[^1];
        bag.RemoveAt(bag.Count - 1);
        text = Loc.Get("fact_prefix") + " " + list[index];
        return true;
    }
}
