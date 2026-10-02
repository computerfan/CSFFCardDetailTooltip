using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CSFFCardDetailTooltip;

public static class EnemyValuesModifiersPreview
{
    public static string Format(EnemyValuesModifiers value, int indent = 0)
    {
        var lines = new List<string>();
        void Add(string key, string name, Vector2 range, EnemyActionEffectCondition condition)
        {
            if (range != Vector2.zero)
                lines.Add(new string(' ', indent) + GenericEncounterPlayerActionPreview.Text(key, name) + ": " + Utils.FormatMinMaxValue(range)
                    + " (" + GenericEncounterPlayerActionPreview.Condition(condition) + ")");
        }
        Add("Melee", "Melee skill", value.MeleeSkillModifier, value.MeleeApplies);
        Add("Ranged", "Ranged skill", value.RangedSkillModifier, value.RangedApplies);
        Add("Blood", "Blood", value.BloodModifier, value.BloodApplies);
        Add("Stamina", "Stamina", value.StaminaModifier, value.StaminaApplies);
        Add("Morale", "Morale", value.MoraleModifier, value.MoraleApplies);
        Add("Value1", "Value 1", value.Value1Modifier, value.Value1Applies);
        Add("Value2", "Value 2", value.Value2Modifier, value.Value2Applies);
        Add("Value3", "Value 3", value.Value3Modifier, value.Value3Applies);
        Add("Value4", "Value 4", value.Value4Modifier, value.Value4Applies);
        return Utils.JoinTooltipLines(lines);
    }
}


