using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CSFFCardDetailTooltip;

public static class PlayerValuesModifierPreview
{
    public static string Format(PlayerValuesModifier value, int indent)
    {
        if (!value.HasAnyEffect) return string.Empty;
        var lines = new List<string>();
        string T(string key, string fallback) => GenericEncounterPlayerActionPreview.Text(key, fallback);
        lines.Add(new string(' ', indent) + T("PlayerCombat", "Player combat modifiers") + ": "
            + (value.WeaponFilter == null || value.WeaponFilter.Length == 0 ? T("AllWeapons", "All weapons") : string.Join(", ", value.WeaponFilter.Select(w => w.TargetName)))
            + (value.ApplyOnlyToEscapeActions ? " (" + T("EscapeOnly", "Escape actions only") + ")" : ""));
        void Add(string key, string name, Vector2 range)
        {
            if (range != Vector2.zero) lines.Add(new string(' ', indent + 2) + T(key, name) + ": " + Utils.FormatMinMaxValue(range));
        }
        Add("ClashIneffectiveRangeMalusModifier", "Ineffective range clash modifier", value.ClashIneffectiveRangeMalusModifier);
        Add("ClashRangedInaccuracyModifier", "Ranged inaccuracy", value.ClashRangedInaccuracyModifier);
        Add("ClashStealthBonusModifier", "Stealth clash bonus", value.ClashStealthBonusModifier);
        Add("ClashVsEscapeModifier", "Clash versus escape", value.ClashVsEscapeModifier);
        Add("ClashVsVulnerableModifier", "Clash versus vulnerable", value.ClashVsVulnerableModifier);
        Add("DamageVsEscapeModifier", "Damage versus escape", value.DamageVsEscapeModifier);
        Add("DmgVsVulnerableModifier", "Damage versus vulnerable", value.DmgVsVulnerableModifier);
        Add("InitialClashValueModifier", "Clash power", value.InitialClashValueModifier);
        Add("InitialDamageModifier", "Damage", value.InitialDamageModifier);
        Add("ReachModifier", "Reach", value.ReachModifier);
        Add("WrestlingClashBonusModifier", "Wrestling clash bonus", value.WrestlingClashBonusModifier);
        Add("WrestlingDamageBonusModifier", "Wrestling damage", value.WrestlingDamageBonusModifier);
        return Utils.JoinTooltipLines(lines);
    }
}



