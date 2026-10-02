using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace CSFFCardDetailTooltip;

// Preview adapter for the original EncounterPlayerDamageReport game type.
public static class EncounterPlayerDamageReportPreview
{
    public static List<Vector2> PlayerDamageRolls(GenericEncounterPlayerAction action, EncounterPopup popup)
    {
        var encounter = popup.CurrentEncounter;
        float fixedDamage = action.ActionRange == ActionRange.Melee ? popup.PlayerSize : 0;
        if (action.DmgFromEnemySizeMult) fixedDamage += encounter.CurrentEnemySize * (float)action.DmgFromEnemySizeMult;
        if (encounter.EncounterModel.DamageTypeAddedDmg != null)
            foreach (var mod in encounter.EncounterModel.DamageTypeAddedDmg)
                if (action.DamageTypes.Contains(mod.DmgType)) fixedDamage += mod.Modifier;
        var rolls = new List<Vector2> { action.InitialDamage, action.DamageStatSum, Vector2.one * fixedDamage };
        if (encounter.Wrestling) rolls.Add(action.WrestlingDamageBonus);
        if (encounter.EnemyVulnerable) rolls.Add(action.DmgVsVulnerableBonus);
        if (encounter.CurrentEnemyAction.IsEscapeAction) rolls.Add(action.DmgVsEscapeModifier);
        if (encounter.CurrentEnemyAction.IsChargingAction) rolls.Add(action.DmgVsChargeBonus);
        return rolls;
    }

    // The game rolls each damage component independently. Their sum is not a uniform range.
    public static double ProbabilityBelow(IReadOnlyList<Vector2> rolls, double value)
    {
        double minimum = rolls.Sum(r => (double)Mathf.Min(r.x, r.y));
        double[] widths = rolls.Select(r => (double)Mathf.Abs(r.y - r.x)).Where(w => w > 0).ToArray();
        double x = value - minimum, total = widths.Sum();
        if (widths.Length == 0) return value > minimum ? 1 : 0;
        if (x <= 0) return 0;
        if (x >= total) return 1;
        // Evaluate the nearer tail to avoid subtracting large, almost equal powers.
        bool upper = x > total / 2;
        if (upper) x = total - x;
        double sum = 0, divisor = 1;
        for (int i = 0; i < widths.Length; i++) divisor *= widths[i] * (i + 1);
        for (int mask = 0; mask < (1 << widths.Length); mask++)
        {
            double term = x;
            int sign = 1;
            for (int i = 0; i < widths.Length; i++)
                if ((mask & (1 << i)) != 0) { term -= widths[i]; sign = -sign; }
            if (term > 0) sum += sign * Math.Pow(term, widths.Length);
        }
        double result = Math.Max(0, Math.Min(1, sum / divisor));
        return upper ? 1 - result : result;
    }

    public static Dictionary<WoundSeverity, double> WoundProbabilities(IReadOnlyList<Vector2> rolls,
        float defense, EncounterPopup popup)
    {
        var result = new Dictionary<WoundSeverity, double>();
        float min = rolls.Sum(r => Mathf.Min(r.x, r.y)), max = rolls.Sum(r => Mathf.Max(r.x, r.y));
        if (min == max) { result[popup.GenerateWoundSeverity(min, defense)] = 1; return result; }
        var bounds = new List<float> { min, max, 0 };
        if (defense > 0 && popup.WoundSeverityMappings != null)
            foreach (var mapping in popup.WoundSeverityMappings)
            { bounds.Add(mapping.AttackDefenseRatio.x * defense); bounds.Add(mapping.AttackDefenseRatio.y * defense); }
        var sorted = bounds.Where(b => b >= min && b <= max).Distinct().OrderBy(b => b).ToArray();
        for (int i = 1; i < sorted.Length; i++)
        {
            var severity = popup.GenerateWoundSeverity((sorted[i - 1] + sorted[i]) / 2, defense);
            double probability = ProbabilityBelow(rolls, sorted[i]) - ProbabilityBelow(rolls, sorted[i - 1]);
            result.TryGetValue(severity, out double previous);
            result[severity] = previous + probability;
        }
        return result;
    }
}
