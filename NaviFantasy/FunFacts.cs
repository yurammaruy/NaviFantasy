using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Dalamud.Plugin.Services;

namespace NaviFantasy;

/// <summary>
/// "Você sabia?" trivia loaded from Assets/curiosidades.txt (one per line, written as "12. Title. Text").
/// Only the text is used. Facts come out in random order and none repeats until all have been told.
/// </summary>
public sealed class FunFacts
{
    private readonly List<string> facts = new();
    private readonly List<int> bag = new();

    public FunFacts(string path, IPluginLog log)
    {
        try
        {
            if (!File.Exists(path))
            {
                log.Warning("Fun facts file not found: {Path}", path);
                return;
            }

            foreach (var raw in File.ReadAllLines(path, Encoding.UTF8))
            {
                var line = raw.Trim();
                if (line.Length == 0)
                    continue;

                // drop the leading "12." and the short title that ends at the first ". "
                var numbered = Regex.Match(line, @"^\d+\s*[\.\)\-]\s*(.+)$");
                var rest = numbered.Success ? numbered.Groups[1].Value : line;
                var cut = rest.IndexOf(". ", StringComparison.Ordinal);
                var body = cut > 0 && cut < 60 ? rest[(cut + 2)..] : rest;
                facts.Add(body.Trim());
            }

            log.Information("Loaded {Count} fun facts.", facts.Count);
        }
        catch (Exception ex)
        {
            log.Error(ex, "Failed to load fun facts.");
        }
    }

    public int Count => facts.Count;

    public bool TryNext(out string text)
    {
        text = string.Empty;
        if (facts.Count == 0)
            return false;

        if (bag.Count == 0)
        {
            for (var i = 0; i < facts.Count; i++)
                bag.Add(i);

            for (var i = bag.Count - 1; i > 0; i--) // shuffle
            {
                var j = Random.Shared.Next(i + 1);
                (bag[i], bag[j]) = (bag[j], bag[i]);
            }
        }

        var index = bag[^1];
        bag.RemoveAt(bag.Count - 1);
        text = "Você sabia? " + facts[index];
        return true;
    }
}
