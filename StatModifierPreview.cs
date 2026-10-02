using System.Collections.Generic;
using UnityEngine;

namespace CSFFCardDetailTooltip;

// Display groups follow the original StatModifier.ApplyEachTick semantics.
public static class StatModifierPreview
{
    public static string Format(IEnumerable<StatModifier> modifiers, int indent = 0)
    {
        if (modifiers == null) return string.Empty;
        var immediate = new List<string>();
        var perTick = new List<string>();
        foreach (var modifier in modifiers)
        {
            string text = FormatValues(modifier, indent + (modifier.ApplyEachTick ? 2 : 0));
            if (string.IsNullOrWhiteSpace(text)) continue;
            (modifier.ApplyEachTick ? perTick : immediate).Add(text);
        }
        if (perTick.Count > 0)
        {
            immediate.Add(Utils.FormatBasicEntry(Utils.LcStr("CSFFCardDetailTooltip.PerTick", "Per action tick"), "", indent: indent));
            immediate.Add(Utils.JoinTooltipLines(perTick));
        }
        return Utils.JoinTooltipLines(immediate);
    }

    private static string FormatValues(StatModifier modifier, int indent)
    {
        if (!modifier.Stat) return string.Empty;
        var lines = new List<string>();
        string name = modifier.Stat.GameName.ToString();
        string source = string.IsNullOrWhiteSpace(modifier.ReportSource) ? "" : $" ({modifier.ReportSource})";
        void Add(Vector2 value, string suffix = "")
        {
            if (value != Vector2.zero)
                lines.Add(Utils.FormatBasicEntry(Utils.FormatMinMaxValue(value), name + suffix + source, indent: indent));
        }
        Add(modifier.ValueModifier);
        Add(modifier.RateModifier, " " + Utils.LcStr("CSFFCardDetailTooltip.Rate", "Rate"));
        Add(modifier.MinValueModifier, $" ({Utils.LcStr("CSFFCardDetailTooltip.Minimum", "Minimum")})");
        Add(modifier.MaxValueModifier, $" ({Utils.LcStr("CSFFCardDetailTooltip.Maximum", "Maximum")})");
        return Utils.JoinTooltipLines(lines);
    }
}
