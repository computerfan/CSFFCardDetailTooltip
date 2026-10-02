using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CSFFCardDetailTooltip;

public static class NPCStatInstantModifierPreview
{
    public static string Text(string key, string fallback) => Utils.LcStr("CSFFCardDetailTooltip.NPC." + key, fallback);

    // Matches GameManager's target loop: an explicit target can select multiple instances.
    public static IEnumerable<InGameNPC> Targets(bool associated, NPCAgent target, InGameNPC context)
    {
        if (!GameManager.Instance || GameManager.Instance.AllNPCs == null) yield break;
        foreach (var npc in GameManager.Instance.AllNPCs)
            if (npc && ((target && npc.NPCModel == target) || (associated && context == npc))) yield return npc;
    }

    public static string Format(IEnumerable<NPCStatInstantModifier> modifiers, InGameNPC context, int indent = 0)
    {
        if (modifiers == null) return string.Empty;
        var lines = new List<string>();
        foreach (var modifier in modifiers)
        {
            if (!modifier.TargetStat || modifier.ValueChange == Vector2.zero) continue;
            foreach (var npc in Targets(modifier.UseAssociatedAgent, modifier.TargetAgent, context))
            {
                var stat = npc.GetStat(modifier.TargetStat);
                if (stat == null) continue; // Native ChangeStat also ignores unavailable stats.
                var scaled = stat.Model.ApplyScaleToModifier(modifier, npc);
                if (scaled.ValueChange == Vector2.zero) continue;
                string qualifier = modifier.TargetStat.UsesNovelty && !modifier.IgnoreNovelty
                    ? " (" + Text("BeforeNovelty", "before repetition and stat limits") + ")"
                    : " (" + Text("BeforeLimits", "before stat limits") + ")";
                lines.Add(Utils.FormatBasicEntry(Utils.FormatMinMaxValue(scaled.ValueChange),
                    npc.GetName() + ": " + modifier.TargetStat.GameName + qualifier, indent: indent));
            }
        }
        return Utils.JoinTooltipLines(lines);
    }
}
