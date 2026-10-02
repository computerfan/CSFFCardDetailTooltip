using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CSFFCardDetailTooltip;

public static class EncounterTemporaryEffectPreview
{
    public static string Format(EncounterTemporaryEffect effect, InGameEncounter encounter, bool subAction, int indent = 0)
    {
        if (effect == null || string.IsNullOrEmpty(effect.EffectID)) return string.Empty;
        string T(string key, string fallback) => GenericEncounterPlayerActionPreview.Text(key, fallback);
        bool refresh = encounter.TemporaryEffects != null && encounter.TemporaryEffects.Any(e => e.EffectID == effect.EffectID);
        string duration = effect.Duration == Vector2Int.zero ? T("UntilEncounterEnds", "Until encounter ends")
            : $"{Mathf.Max(1, effect.Duration.x)}–{Mathf.Max(1, effect.Duration.y)} " + T("Rounds", "rounds");
        var lines = new List<string>
        {
            new string(' ', indent) + T("TemporaryEffect", "Temporary effect") + $": {effect.EffectID} ({GenericEncounterPlayerActionPreview.Condition(effect.EffectApplies)}; {duration})"
        };
        if (subAction && effect.Duration != Vector2Int.zero) lines.Add(new string(' ', indent + 2) + T("SubActionDuration", "Sub-action: duration reduced by one immediately"));
        if (refresh)
            lines.Add(new string(' ', indent + 2) + T("RefreshDuration", "Refreshes duration; does not stack values"));
        else
        {
            lines.Add(StatModifierPreview.Format(effect.StatEffects, indent + 2));
            lines.Add(EnemyValuesModifiersPreview.Format(effect.EnemyValuesModifiers, indent + 2));
            // 0.68b BodyLocationModifiers.Instantiate returns default: no body changes survive.
            if (effect.PlayerValuesModifiers != null)
                foreach (var source in effect.PlayerValuesModifiers)
                {
                    // 0.68b PlayerValuesModifier.Instantiate does not copy these restrictions.
                    var modifier = source;
                    modifier.WeaponFilter = null;
                    modifier.ApplyOnlyToEscapeActions = false;
                    lines.Add(PlayerValuesModifierPreview.Format(modifier, indent + 2));
                }
        }
        return Utils.JoinTooltipLines(lines);
    }
}


